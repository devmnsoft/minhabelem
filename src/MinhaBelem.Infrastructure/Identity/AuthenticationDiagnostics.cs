using Dapper;
using MySqlConnector;
using System.Security.Cryptography;
using System.Text;

namespace MinhaBelem.Infrastructure.Identity;

public static class AuthenticationDiagnostics
{
    public static string DatabaseReference(string connection)
    {
        var cs = new MySqlConnectionStringBuilder(connection);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{cs.Server}|{cs.Port}|{cs.Database}")))[..12];
    }
    public static async Task<object> ReadLocalAsync(MySqlDataSource data, CancellationToken ct = default)
    {
        var cs = new MySqlConnectionStringBuilder(data.ConnectionString);
        if (cs.Server is not ("localhost" or "127.0.0.1")) throw new InvalidOperationException("Diagnóstico exclusivo do banco local.");
        await using var db = await data.OpenConnectionAsync(ct);
        var accounts = await db.QueryAsync(new CommandDefinition("""
            SELECT email,enabled,must_change_password AS mustChangePassword,two_factor_enabled AS twoFactorEnabled,
                password_hash IS NOT NULL AS hasPasswordHash,security_stamp IS NOT NULL AS hasSecurityStamp,
                concurrency_stamp IS NOT NULL AS hasConcurrencyStamp,BINARY normalized_email=BINARY UPPER(TRIM(email)) AS normalizedEmailMatches,
                access_failed_count AS failedAttempts,lockout_end>UTC_TIMESTAMP(6) AS lockedOut,
                (SELECT COUNT(*) FROM mb_user_sessions s WHERE s.user_id=u.id AND revoked_at IS NULL AND expires_at>UTC_TIMESTAMP(6)) AS activeSessions
            FROM mb_users u WHERE email IN ('superadmin@minhabelem.com.br','editor.teste@minhabelem.com.br','moderador.teste@minhabelem.com.br')
            """, cancellationToken: ct));
        return new { databaseReference = DatabaseReference(data.ConnectionString),
            version = await db.ExecuteScalarAsync<string>("SELECT VERSION()"), accounts };
    }
}
