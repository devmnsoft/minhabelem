using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class AdministrationRepository(MySqlDataSource data) : IAdministrationRepository
{
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
