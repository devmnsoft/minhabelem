using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Policies;

namespace MinhaBelem.Infrastructure.Background;

/// <summary>
/// Manutenção periódica: expurga do cache do guia (política do Google Places), sinaliza
/// conteúdo desatualizado e arquiva o que venceu a validade.
/// </summary>
public sealed class MaintenanceWorker(IServiceScopeFactory scopeFactory, ILogger<MaintenanceWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (true)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha na manutenção periódica");
            }

            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) return; }
            catch (OperationCanceledException) { return; }
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;
        var now = DateTime.UtcNow;

        var places = provider.GetRequiredService<IPlaceRepository>();
        var purged = await places.PurgeExpiredAsync(now, ct);
        if (purged > 0) logger.LogInformation("Expurgo do guia: {Count} lugares fora do prazo de cache removidos.", purged);

        var content = provider.GetRequiredService<IContentRepository>();
        var expired = await content.ExpireOldContentAsync(now, ct);
        if (expired > 0) logger.LogInformation("Manutenção: {Count} conteúdos expirados foram arquivados.", expired);

        var sources = provider.GetRequiredService<IIngestionRepository>();
        var all = await sources.SourcesAsync(onlyOperational: false, ct);
        foreach (var source in all.Where(s => s.ConsecutiveFailures >= 10 && s.Enabled))
        {
            await sources.UpdateSourceRuntimeAsync(source.Id, now, null,
                $"Fonte desativada automaticamente após {source.ConsecutiveFailures} falhas consecutivas.", null, null, ct);
            logger.LogWarning("Fonte {Source} desativada por falhas consecutivas.", source.Name);
        }
    }
}

/// <summary>Marca como desatualizado o conteúdo que passou da janela editorial definida.</summary>
public static class FreshnessMarker
{
    public static bool ShouldMarkOutdated(Core.Entities.ContentItem item, DateTime now) =>
        item.Status == Core.Entities.ContentStatus.Published &&
        item.VerificationStatus == Core.Entities.VerificationStatus.Verified &&
        FreshnessPolicy.IsOutdated(item, now);
}