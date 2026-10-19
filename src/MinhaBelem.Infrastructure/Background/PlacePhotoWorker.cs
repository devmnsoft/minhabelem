using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Infrastructure.Discovery;

namespace MinhaBelem.Infrastructure.Background;

/// <summary>Consulta o Wikimedia Commons, em ritmo baixo, pelos pontos cadastrados da região metropolitana.</summary>
public sealed class PlacePhotoWorker(IServiceScopeFactory scopes, IOptions<PhotoScoutOptions> options, ILogger<PlacePhotoWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<IPlacePhotoScout>().ScoutNextAsync(settings.TargetsPerRun, stoppingToken);
                logger.LogInformation("Busca de fotos de locais concluiu {Count} gravações neste ciclo.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Busca de fotos de locais indisponível neste ciclo: {ErrorType}", ex.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromHours(Math.Clamp(settings.IntervalHours, 1, 168)), stoppingToken);
        }
    }
}
