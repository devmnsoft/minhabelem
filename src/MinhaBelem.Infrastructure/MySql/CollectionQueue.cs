using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Security;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class CollectionQueue(MySqlDataSource data) : ICollectionQueue
{
    public async Task<long> EnqueueAsync(string kind, string? targetUrl, long actorId, CancellationToken ct)
    {
        if (kind is not ("feeds" or "radar" or "target")) throw new ArgumentException("Tipo de coleta inválido.", nameof(kind));
        if (kind == "target" && (targetUrl is null || targetUrl.Length > 1000 || !SafeUrl.Validate(targetUrl).IsValid))
            throw new ArgumentException("URL pública HTTPS obrigatória.", nameof(targetUrl));
        if (targetUrl is not null && Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(targetUrl).Query).Keys
            .Any(key => key.ToLowerInvariant() is "key" or "token" or "access_token" or "api_key" or "password"))
            throw new ArgumentException("Use referências seguras para credenciais.", nameof(targetUrl));
        await using var db = await data.OpenConnectionAsync(ct);
        return await db.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO collection_requests (kind, target_url, requested_by_id) VALUES (@kind, @targetUrl, @actorId);
            SELECT LAST_INSERT_ID();
            """, new { kind, targetUrl = kind == "target" ? targetUrl : null, actorId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<CollectionRequest>> RecentAsync(CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        return (await db.QueryAsync<CollectionRequest>(new CommandDefinition("""
            SELECT id, kind, status, attempts, requested_at AS RequestedAt, finished_at AS FinishedAt, error
            FROM collection_requests ORDER BY id DESC LIMIT 30
            """, cancellationToken: ct))).AsList();
    }
}
