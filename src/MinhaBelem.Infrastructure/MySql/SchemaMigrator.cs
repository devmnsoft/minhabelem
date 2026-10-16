using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.MySql;

public sealed record MigrationResult(IReadOnlyList<string> Applied, IReadOnlyList<string> AlreadyPresent, TimeSpan Duration);

/// <summary>
/// Instalador do esquema unificado. Lê os scripts embarcados no assembly, ordena pelo nome e registra
/// o que aplicou em <c>schema_migrations</c>. Seguro para execução concorrente (GET_LOCK).
/// </summary>
public sealed class SchemaMigrator(MySqlDataSource dataSource, ILogger<SchemaMigrator> logger)
{
    public const string LockName = "minhabelem_schema_migration";

    public async Task<MigrationResult> ApplyAsync(CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var scripts = LoadScripts();
        if (scripts.Count == 0) return new MigrationResult(Array.Empty<string>(), Array.Empty<string>(), TimeSpan.Zero);

        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var applied = new List<string>();
        var skipped = new List<string>();

        await using var lockConn = await dataSource.OpenConnectionAsync(ct);
        var acquired = Convert.ToInt32(await lockConn.ExecuteScalarAsync(new CommandDefinition(
            "SELECT GET_LOCK(@name, 30);", new { name = LockName }, cancellationToken: ct)));
        if (acquired != 1)
            throw new InvalidOperationException("Não foi possível obter o bloqueio de migration (GET_LOCK).");

        try
        {
            var hasHistory = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = 'schema_migrations'",
                cancellationToken: ct)) > 0;
            var known = hasHistory
                ? (await connection.QueryAsync<(string Version, string Checksum)>(new CommandDefinition(
                    "SELECT version AS Version, checksum AS Checksum FROM schema_migrations", cancellationToken: ct)))
                    .ToDictionary(x => x.Version, x => x.Checksum, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var (version, script) in scripts)
            {
                var checksum = Checksum(script);
                if (known.TryGetValue(version, out var previousChecksum))
                {
                    if (!string.Equals(previousChecksum, checksum, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"A migration '{version}' já foi aplicada com conteúdo diferente. Crie uma nova migration em vez de editar uma aplicada.");
                    skipped.Add(version);
                    continue;
                }

                var migrationStart = DateTime.UtcNow;
                // MySQL faz commit implicito em DDL; nao ha rollback transacional do esquema.
                await connection.ExecuteAsync(new CommandDefinition(script, cancellationToken: ct));
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO schema_migrations (version, checksum, duration_ms) VALUES (@version, @checksum, @ms);",
                    new { version, checksum, ms = (int)(DateTime.UtcNow - migrationStart).TotalMilliseconds },
                    cancellationToken: ct));

                applied.Add(version);
                logger.LogInformation("Migration aplicada: {Version}", version);
            }
        }
        finally
        {
            await lockConn.ExecuteAsync(new CommandDefinition("SELECT RELEASE_LOCK(@name);", new { name = LockName }, cancellationToken: ct));
        }

        return new MigrationResult(applied, skipped, DateTime.UtcNow - started);
    }

    private static string Checksum(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized)));
    }

    /// <summary>Le a unica fonte SQL, embarcada diretamente da raiz do projeto.</summary>
    internal static IReadOnlyList<(string Version, string Script)> LoadScripts(Assembly? assembly = null)
    {
        assembly ??= typeof(SchemaMigrator).Assembly;
        using var stream = assembly.GetManifestResourceStream("MinhaBelem.Infrastructure.MINHABELEM.SQL")
            ?? throw new InvalidOperationException("Recurso MINHABELEM.SQL nao encontrado.");
        using var reader = new StreamReader(stream);
        var parts = System.Text.RegularExpressions.Regex.Split(reader.ReadToEnd(), @"(?m)^-- @migration ([a-zA-Z0-9_]+)[ \t]*\r?$");
        var scripts = new List<(string Version, string Script)> { ("MINHABELEM", parts[0]) };
        for (var i = 1; i < parts.Length; i += 2) scripts.Add((parts[i], parts[i + 1]));
        return scripts;
    }
}
