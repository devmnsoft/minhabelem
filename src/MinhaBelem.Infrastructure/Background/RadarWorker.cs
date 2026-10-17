using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
using MinhaBelem.Infrastructure.MySql;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.Background;

public interface IRadarWorker
{
    Task TriggerOnceAsync(CancellationToken ct);
}

public sealed class RadarWorker(
    IServiceScopeFactory scopes,
    IOptions<CollectorOptions> options,
    MySqlDataSource data,
    ILogger<RadarWorker> logger) : BackgroundService, IRadarWorker
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var last = DateTime.MinValue;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var admin = scope.ServiceProvider.GetRequiredService<IAdministrationRepository>();
                var radar = scope.ServiceProvider.GetRequiredService<IRadarRepository>();
                var settings = await admin.SettingsAsync(ct);

                var enabled = bool.TryParse(settings.GetValueOrDefault("radar.enabled"), out var v) ? v : options.Value.Enabled;
                var minutes = int.TryParse(settings.GetValueOrDefault("radar.intervalMinutes"), out var m) ? Math.Clamp(m, 1, 180) : Math.Clamp(options.Value.IntervalMinutes, 1, 180);

                // Atualiza batimento cardíaco (Heartbeat) periódico mesmo enquanto ocioso
                await radar.UpdateHeartbeatAsync("RadarWorker", "idle", $"Próximo ciclo programado para {last.AddMinutes(minutes).ToLocalTime():HH:mm:ss}", null, isRunning: false, ct);

                if (enabled && DateTime.UtcNow >= last.AddMinutes(minutes))
                {
                    await TriggerOnceAsync(ct);
                    last = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("RadarWorker erro no loop periódico: {Message}", ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async Task TriggerOnceAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(55));
        var token = timeout.Token;

        await using var conn = await data.OpenConnectionAsync(token);
        var acquired = await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT GET_LOCK('minhabelem_radar', 0)", cancellationToken: token));
        if (acquired != 1)
        {
            logger.LogInformation("RadarWorker: Bloqueio 'minhabelem_radar' ocupado por outra instância.");
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var radar = scope.ServiceProvider.GetRequiredService<IRadarRepository>();
        var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionRepository>();
        var crawler = scope.ServiceProvider.GetRequiredService<IPublicWebCrawler>();

        var startedAt = DateTime.UtcNow;
        var sourcesChecked = 0;
        var sourcesFailed = 0;
        var totalItems = 0;
        var totalEvents = 0;
        string? cycleError = null;

        try
        {
            await radar.UpdateHeartbeatAsync("RadarWorker", "running", "Varredura autônoma em execução...", null, isRunning: true, token);

            var sources = await ingestion.SourcesAsync(onlyOperational: true, token);
            var eligibleSources = sources.Where(s => s.Protocol is FeedProtocol.Html or FeedProtocol.WebDiscovery or FeedProtocol.JsonLd).ToList();

            foreach (var source in eligibleSources)
            {
                sourcesChecked++;
                try
                {
                    var crawlOptions = new CrawlerOptions
                    {
                        MaxDepth = 2,
                        MaxPagesPerHost = 15,
                        DelayBetweenRequestsMs = 300
                    };

                    var result = await crawler.CrawlAsync(source, crawlOptions, token);
                    totalItems += result.ItemsDiscovered;
                    totalEvents += result.EventsIdentified;

                    logger.LogInformation("Radar crawled {Source}: {Urls} urls, {Items} items, {Events} events.",
                        source.Name, result.UrlsCrawled, result.ItemsDiscovered, result.EventsIdentified);

                    await ingestion.UpdateSourceRuntimeAsync(source.Id, DateTime.UtcNow, DateTime.UtcNow, null, null, null, token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    sourcesFailed++;
                    logger.LogWarning("Radar erro ao rastrear fonte {Source}: {Error}", source.Name, ex.Message);
                    await ingestion.UpdateSourceRuntimeAsync(source.Id, DateTime.UtcNow, null, ex.Message, null, null, token);
                }
            }

            // Processa candidatos para IA e fila editorial (Descoberto -> Extraído -> Classificado -> Revisão)
            await ProcessPendingRadarCandidatesAsync(scope.ServiceProvider, token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            cycleError = ex.Message;
            throw;
        }
        finally
        {
            var duration = DateTime.UtcNow - startedAt;
            var finalStatus = cycleError is not null ? "failed" : sourcesFailed == 0 ? "success" : sourcesFailed == sourcesChecked ? "failed" : "partial";
            var summary = $"{sourcesChecked} fontes ({sourcesFailed} falhas), {totalItems} matérias e {totalEvents} eventos em {duration.TotalSeconds:F1}s.";

            await radar.UpdateHeartbeatAsync("RadarWorker", finalStatus, summary, cycleError, isRunning: false, CancellationToken.None);
            await conn.ExecuteAsync("SELECT RELEASE_LOCK('minhabelem_radar')");
        }
    }

    private static async Task ProcessPendingRadarCandidatesAsync(IServiceProvider services, CancellationToken ct)
    {
        var ingestion = services.GetRequiredService<IIngestionRepository>();
        var discovery = services.GetRequiredService<IDiscoveryRepository>();
        var content = services.GetRequiredService<IContentRepository>();
        var editorial = services.GetRequiredService<IEditorialWorkflow>();
        var ai = services.GetRequiredService<IAiAnalysisService>();

        var pendingCandidates = await ingestion.CandidatesAsync(new CandidateQuery
        {
            Status = CandidateStatus.Pending,
            PageSize = 50,
            OperationalOnly = true,
            OnlyUnimported = true
        }, ct);

        foreach (var candidate in pendingCandidates)
        {
            if (candidate.ContentId is not null) continue;

            var source = await ingestion.SourceAsync(candidate.SourceId, ct);
            if (source is null || !source.IsOperational) continue;

            var draftId = await editorial.ImportAsync(candidate.Id, null, ct);
            if (draftId is null) continue;

            var draft = await content.GetAsync(draftId.Value, ct);
            if (draft is null || draft.Status != ContentStatus.InReview) continue;

            var comparisons = (await content.SearchForDuplicatesAsync(draft.Title, draft.Summary, 6, ct))
                .Where(x => x.Id != draftId)
                .ToArray();

            var aiResult = await ai.AnalyzeAsync(new AiAnalysisInput(draft.Title, draft.Summary, draft.SourceUrl, comparisons)
            {
                TargetId = draftId
            }, ct);

            if (aiResult.Classification is { } classification)
            {
                draft.Category = classification.Category;
                draft.Subject = classification.Subject;
                draft.Neighborhood = classification.Neighborhood;
                draft.Municipality = classification.Municipality;
                draft.Audiences = classification.Audiences;
                draft.Flags |= classification.Flags;
            }

            if (aiResult.IsDuplicate)
            {
                draft.Flags |= ModerationFlag.PossibleDuplicate;
                draft.DuplicateOfId = aiResult.DuplicateOfId;
            }

            draft.EventDetails = await discovery.EvidenceAsync(candidate.Id, ct) ?? aiResult.Event;
            if (draft.EventDetails is { } ev)
            {
                draft.Kind = ContentKind.Event;
                draft.EventStartAt = ev.StartUtc;
                draft.EventEndAt = ev.EndUtc;
                draft.Address = ev.Location;
                draft.IsFree = ev.IsFree;
                if (ev.Uncertain) draft.Flags |= ModerationFlag.Sensitive;
            }

            draft.RequiresHumanApproval = true;
            if (await content.UpdateAsync(draft, null, "Radar autônomo: Descoberto, extraído e classificado por IA; aguarda revisão editorial humana", ct))
            {
                await ingestion.SetCandidateStatusAsync(candidate.Id, CandidateStatus.Approved, null, "Preparado para revisão editorial", ct);
            }
        }
    }
}
