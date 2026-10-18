using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class AdministrationRepository(MySqlDataSource data) : IAdministrationRepository
{
    public async Task<IReadOnlyList<string>> PermissionsAsync(long userId, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        var roles = await db.QueryAsync<string>(new CommandDefinition("SELECT r.name FROM mb_roles r JOIN mb_user_roles ur ON ur.role_id=r.id WHERE ur.user_id=@userId", new { userId }, cancellationToken: ct));
        var matrix = await ReadRolePermissionsAsync(db, ct);
        return roles.SelectMany(role => matrix.GetValueOrDefault(role) ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
    }
    public async Task<IReadOnlyDictionary<string,string[]>> RolePermissionsAsync(CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        return await ReadRolePermissionsAsync(db, ct);
    }
    private static async Task<IReadOnlyDictionary<string,string[]>> ReadRolePermissionsAsync(MySqlConnection db, CancellationToken ct)
    {
        var rows = await db.QueryAsync<(string Role,string Permission)>(new CommandDefinition("SELECT r.name AS Role,rp.permission_code AS Permission FROM mb_roles r JOIN mb_role_permissions rp ON rp.role_id=r.id", cancellationToken: ct));
        var matrix = rows.GroupBy(x => x.Role).ToDictionary(g => g.Key, g => g.Select(x => x.Permission).ToArray());
        var overrides = await db.QueryAsync<(string Key,string Value)>(new CommandDefinition("SELECT setting_key AS `Key`,setting_value AS Value FROM mb_settings WHERE setting_key LIKE 'role.permissions.%'", cancellationToken: ct));
        foreach (var entry in overrides)
        {
            var role = entry.Key["role.permissions.".Length..];
            if (role == Core.Security.Roles.SuperAdmin) continue;
            try { matrix[role] = System.Text.Json.JsonSerializer.Deserialize<string[]>(entry.Value) ?? Array.Empty<string>(); }
            catch (System.Text.Json.JsonException) { matrix[role] = Array.Empty<string>(); }
        }
        return matrix;
    }
    public async Task SetRolePermissionsAsync(string role, string[] permissions, long actorId, CancellationToken ct)
    {
        if (role == Core.Security.Roles.SuperAdmin || !Core.Security.Roles.All.Contains(role) || permissions.Any(x => !Core.Security.Permissions.All.Contains(x))) throw new InvalidOperationException("Perfil ou permissão não permitidos.");
        await using var db = await data.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var roleId = await db.ExecuteScalarAsync<long>(new CommandDefinition("SELECT id FROM mb_roles WHERE normalized_name=@name FOR UPDATE", new { name = role.ToUpperInvariant() }, tx, cancellationToken: ct));
        // Guardar a configuracao explicita separada dos defaults do instalador preserva as
        // revogacoes mesmo quando MINHABELEM.SQL e executado novamente.
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_settings (setting_key,setting_value) VALUES (@key,@value) ON DUPLICATE KEY UPDATE setting_value=VALUES(setting_value)", new { key = "role.permissions." + role, value = System.Text.Json.JsonSerializer.Serialize(permissions.Distinct()) }, tx, cancellationToken: ct));
        await db.ExecuteAsync(new CommandDefinition("UPDATE mb_users u JOIN mb_user_roles ur ON ur.user_id=u.id SET u.security_stamp=@stamp,u.concurrency_stamp=@concurrency WHERE ur.role_id=@roleId", new { roleId, stamp = Guid.NewGuid().ToString("N"), concurrency = Guid.NewGuid().ToString("N") }, tx, cancellationToken: ct));
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_audit_logs (actor_id,action,entity_type,entity_id,details) VALUES (@actorId,'role.permissions_changed','role',@roleId,@details)", new { actorId, roleId, details = System.Text.Json.JsonSerializer.Serialize(new { role, permissions }) }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }
    public async Task SetMediaPermissionAsync(long sourceId, bool allowed, string license, long actorId, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_media_permissions (source_id,allow_analysis,license_note) VALUES (@sourceId,@allowed,@license) ON DUPLICATE KEY UPDATE allow_analysis=VALUES(allow_analysis),license_note=VALUES(license_note)", new { sourceId, allowed, license }, tx, cancellationToken: ct));
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_audit_logs (actor_id,action,entity_type,entity_id,details) VALUES (@actorId,'media.permission_changed','source',@sourceId,JSON_OBJECT('allowed',@allowed))", new { actorId, sourceId, allowed }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<AdminUser>> UsersAsync(CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        return (await db.QueryAsync<AdminUser>(new CommandDefinition("SELECT id AS Id, email AS Email, display_name AS DisplayName, enabled AS Enabled, two_factor_enabled AS TwoFactorEnabled FROM mb_users ORDER BY id LIMIT 200", cancellationToken: ct))).AsList();
    }
    public async Task<IReadOnlyList<UserSession>> SessionsAsync(long userId, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        return (await db.QueryAsync<UserSession>(new CommandDefinition("SELECT id AS Id, user_id AS UserId, created_at AS CreatedAt, expires_at AS ExpiresAt, revoked_at AS RevokedAt FROM mb_user_sessions WHERE user_id=@userId ORDER BY created_at DESC LIMIT 100", new { userId }, cancellationToken: ct))).AsList();
    }
    public async Task CreateSessionAsync(string id, long userId, DateTime expires, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_user_sessions (id,user_id,expires_at) VALUES (@id,@userId,@expires)", new { id, userId, expires }, cancellationToken: ct));
    }
    public async Task<bool> SessionActiveAsync(string id, long userId, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        return await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_user_sessions WHERE id=@id AND user_id=@userId AND revoked_at IS NULL AND expires_at>UTC_TIMESTAMP(6)", new { id, userId }, cancellationToken: ct)) == 1;
    }
    public async Task RevokeSessionAsync(string id, long userId, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("UPDATE mb_user_sessions SET revoked_at=UTC_TIMESTAMP(6) WHERE id=@id AND user_id=@userId", new { id, userId }, cancellationToken: ct));
    }
    public async Task RevokeAllSessionsAsync(long userId, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("UPDATE mb_user_sessions SET revoked_at=UTC_TIMESTAMP(6) WHERE user_id=@userId AND revoked_at IS NULL", new { userId }, cancellationToken: ct));
    }
    public async Task<IReadOnlyDictionary<string, string>> SettingsAsync(CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        return (await db.QueryAsync<(string Key, string Value)>(new CommandDefinition("SELECT setting_key AS `Key`, setting_value AS Value FROM mb_settings", cancellationToken: ct))).ToDictionary(x => x.Key, x => x.Value);
    }
    public async Task SetSettingAsync(string key, string value, long actorId, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_settings (setting_key,setting_value) VALUES (@key,@value) ON DUPLICATE KEY UPDATE setting_value=VALUES(setting_value)", new { key, value }, tx, cancellationToken: ct));
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_audit_logs (actor_id,action,entity_type,details) VALUES (@actorId,'settings.updated','settings',@details)", new { actorId, details = System.Text.Json.JsonSerializer.Serialize(new { key, value }) }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<IngestionRun>> RunsAsync(CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        return (await db.QueryAsync<IngestionRun>(new CommandDefinition("SELECT id AS Id, started_at AS StartedAt, finished_at AS FinishedAt, status AS Status, sources_checked AS SourcesChecked,sources_failed AS SourcesFailed,items_seen AS ItemsSeen,items_inserted AS ItemsInserted,items_duplicate AS ItemsDuplicate,error AS Error FROM mb_ingestion_runs ORDER BY id DESC LIMIT 50", cancellationToken: ct))).AsList();
    }
}
