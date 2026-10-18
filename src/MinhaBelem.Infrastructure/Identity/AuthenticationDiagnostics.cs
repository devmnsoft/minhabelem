using Dapper;
using MySqlConnector;
using System.Security.Cryptography;
using System.Text;
using MinhaBelem.Core.Security;
using MinhaBelem.Infrastructure.MySql;

namespace MinhaBelem.Infrastructure.Identity;

public static class AuthenticationDiagnostics
{
    public static string DatabaseReference(string connection)
    {
        var cs = new MySqlConnectionStringBuilder(connection);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{cs.Server}|{cs.Port}|{cs.Database}")))[..12];
    }
    public static async Task<object> ReadLocalAsync(MySqlDataSource data, CancellationToken ct = default, string? environment = null)
    {
        var cs = new MySqlConnectionStringBuilder(data.ConnectionString);
        if (cs.Server is not ("localhost" or "127.0.0.1")) throw new InvalidOperationException("Diagnóstico exclusivo do banco local.");
        await using var db = await data.OpenConnectionAsync(ct);
        var rows = (await db.QueryAsync(new CommandDefinition("""
            SELECT id,email,enabled,must_change_password AS mustChangePassword,two_factor_enabled AS twoFactorEnabled,
                password_hash IS NOT NULL AS hasPasswordHash,security_stamp IS NOT NULL AS hasSecurityStamp,
                concurrency_stamp IS NOT NULL AS hasConcurrencyStamp,BINARY normalized_email=BINARY UPPER(TRIM(email)) AS normalizedEmailMatches,
                access_failed_count AS failedAttempts,COALESCE(lockout_enabled AND lockout_end>UTC_TIMESTAMP(6),FALSE) AS lockedOut,
                (SELECT COUNT(*) FROM mb_user_sessions s WHERE s.user_id=u.id AND revoked_at IS NULL AND expires_at>UTC_TIMESTAMP(6)) AS activeSessions
            FROM mb_users u WHERE email IN ('superadmin@minhabelem.com.br','editor.teste@minhabelem.com.br','moderador.teste@minhabelem.com.br')
                OR normalized_email IN ('SUPERADMIN@MINHABELEM.COM.BR','EDITOR.TESTE@MINHABELEM.COM.BR','MODERADOR.TESTE@MINHABELEM.COM.BR')
            """, cancellationToken: ct))).ToArray();
        var accounts = new List<object>();
        var admin = new AdministrationRepository(data);
        foreach (var (email, expectedRole) in LocalCredentialRecovery.Accounts)
        {
            var row = rows.SingleOrDefault(x => string.Equals((string)x.email, email, StringComparison.OrdinalIgnoreCase));
            if (row is null) { accounts.Add(new { email, exists = false, status = "account_absent", expectedRole }); continue; }
            long id = Convert.ToInt64(row.id);
            var roles = (await db.QueryAsync<string>(new CommandDefinition("SELECT r.name FROM mb_roles r JOIN mb_user_roles ur ON ur.role_id=r.id WHERE ur.user_id=@id",new {id},cancellationToken:ct))).Order().ToArray();
            var permissions = (await admin.PermissionsAsync(id,ct)).Order().ToArray();
            bool enabled=Convert.ToBoolean(row.enabled), locked=Convert.ToBoolean(row.lockedOut), mustChange=Convert.ToBoolean(row.mustChangePassword), mfa=Convert.ToBoolean(row.twoFactorEnabled);
            var attempts = await db.QueryAsync(new CommandDefinition("""
                SELECT created_at AS occurredAt,JSON_UNQUOTE(JSON_EXTRACT(details,'$.stage')) AS stage,
                    JSON_UNQUOTE(JSON_EXTRACT(details,'$.reason')) AS reason
                FROM mb_audit_logs WHERE entity_type='user' AND entity_id=@id AND action='auth.attempt'
                ORDER BY id DESC LIMIT 10
                """,new {id},cancellationToken:ct));
            accounts.Add(new { email, exists=true, expectedRole, enabled, mustChangePassword=mustChange, lockedOut=locked, twoFactorEnabled=mfa,
                status=!enabled?"disabled":locked?"locked_out":mustChange?"password_change_required":!mfa?"mfa_setup_required":"ready_for_password_and_mfa",
                row.hasPasswordHash,row.hasSecurityStamp,row.hasConcurrencyStamp,row.normalizedEmailMatches,row.failedAttempts,row.activeSessions,
                roles, permissions, expectedRolePresent=roles.Contains(expectedRole),
                missingDefaultPermissions=Permissions.ByRole[expectedRole].Except(permissions).Order().ToArray(), recentAttempts=attempts });
        }
        return new { host=cs.Server,port=cs.Port,database=cs.Database,environment=environment??"not_supplied",databaseReference = DatabaseReference(data.ConnectionString),
            version = await db.ExecuteScalarAsync<string>("SELECT VERSION()"), accounts };
    }
}
