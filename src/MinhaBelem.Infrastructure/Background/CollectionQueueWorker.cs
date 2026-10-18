using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;

namespace MinhaBelem.Infrastructure.Background;

/// <summary>Only registered in the service host. The connection owns the dispatch lock until completion.</summary>
public sealed class CollectionQueueWorker(IServiceScopeFactory scopes, MySqlDataSource data, ILogger<CollectionQueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessNextAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning("Fila indisponível: {ErrorType}", ex.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        if (await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT GET_LOCK('minhabelem_collection_queue',0)", cancellationToken: ct)) != 1) return false;
        try
        {
            // A previous owner cannot still be executing while this connection holds the lock.
            await db.ExecuteAsync(new CommandDefinition("""
                UPDATE collection_requests SET status = IF(attempts < 3, 'pending', 'failed'),
                    error = 'Execução interrompida; recuperação pelo serviço.',
                    finished_at = IF(attempts < 3, NULL, UTC_TIMESTAMP(6))
                WHERE status = 'running';
                """, cancellationToken: ct));
            var request = await db.QuerySingleOrDefaultAsync<CollectionRequest>(new CommandDefinition("""
                SELECT id, kind, target_url AS TargetUrl FROM collection_requests
                WHERE status = 'pending' AND available_at <= UTC_TIMESTAMP(6) ORDER BY id LIMIT 1
                """, cancellationToken: ct));
            if (request is null) return false;
            await db.ExecuteAsync(new CommandDefinition("UPDATE collection_requests SET status='running', attempts=attempts+1, started_at=UTC_TIMESTAMP(6) WHERE id=@Id", request, cancellationToken: ct));
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                if (request.Kind == "feeds") await scope.ServiceProvider.GetRequiredService<ISourceCollection>().CollectOnceAsync(timeout.Token);
                else if (request.Kind == "radar") await scope.ServiceProvider.GetRequiredService<IRadarWorker>().TriggerOnceAsync(timeout.Token);
                else if (request.Kind == "target")
                {
                    var result = await scope.ServiceProvider.GetRequiredService<IPublicWebCrawler>().CrawlTargetUrlAsync(request.TargetUrl!, new CrawlerOptions { MaxDepth = 1, MaxPagesPerHost = 5 }, timeout.Token);
                    if (result.Errors.Count > 0) throw new InvalidOperationException("Falha na consulta da fonte.");
                }
                else throw new InvalidOperationException("Tipo de solicitação desconhecido.");
                await db.ExecuteAsync(new CommandDefinition("UPDATE collection_requests SET status='completed', finished_at=UTC_TIMESTAMP(6), error=NULL WHERE id=@Id", request, cancellationToken: CancellationToken.None));
            }
            catch (Exception ex)
            {
                // Never persist external exception messages, which may contain credentials or URLs.
                await db.ExecuteAsync(new CommandDefinition("""
                    UPDATE collection_requests SET status=IF(attempts<3,'pending','failed'),
                        available_at=DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 1 MINUTE),
                        finished_at=IF(attempts<3,NULL,UTC_TIMESTAMP(6)), error=@error WHERE id=@Id
                    """, new { request.Id, error = "Coleta interrompida: " + ex.GetType().Name }, cancellationToken: CancellationToken.None));
                if (ct.IsCancellationRequested) throw;
            }
            return true;
        }
        finally { await db.ExecuteAsync("SELECT RELEASE_LOCK('minhabelem_collection_queue')"); }
    }
}
