using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Services;

namespace MinhaBelem.Infrastructure.Background;

public sealed class FeedCollectorWorker(IServiceScopeFactory scopes, IOptions<CollectorOptions> options, MySqlDataSource data, ILogger<FeedCollectorWorker> logger) : BackgroundService, ISourceCollection
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var last = DateTime.MinValue;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var settings = await scope.ServiceProvider.GetRequiredService<IAdministrationRepository>().SettingsAsync(ct);
                var enabled = bool.TryParse(settings.GetValueOrDefault("collector.enabled"), out var value) ? value : options.Value.Enabled;
                var minutes = int.TryParse(settings.GetValueOrDefault("collector.intervalMinutes"), out var interval) ? Math.Clamp(interval, 1, 240) : Math.Clamp(options.Value.IntervalMinutes, 1, 240);
                if (enabled && DateTime.UtcNow >= last.AddMinutes(minutes)) { last = DateTime.UtcNow; await CollectOnceAsync(ct); }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning("Coletor indisponível: {ErrorType}. Confira esquema e configurações.", ex.GetType().Name); }
            try { if (!await timer.WaitForNextTickAsync(ct)) return; } catch (OperationCanceledException) { return; }
        }
    }
    public async Task CollectOnceAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(55));
        ct = timeout.Token;
        await using var lease = await data.OpenConnectionAsync(ct);
        if (await lease.ExecuteScalarAsync<int>(new CommandDefinition("SELECT GET_LOCK('minhabelem_collector',0)", cancellationToken: ct)) != 1) throw new InvalidOperationException("Outra instância está executando o coletor.");
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IIngestionRepository>();
            var run = new IngestionRun { Id = await repository.StartRunAsync(ct), StartedAt = DateTime.UtcNow, Status = RunStatus.Running };
            try
            {
                var sources = (await repository.SourcesAsync(true, ct)).Where(s => s.Protocol is not (FeedProtocol.Html or FeedProtocol.WebDiscovery or FeedProtocol.JsonLd));
                await Parallel.ForEachAsync(sources, new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(options.Value.MaxConcurrentSources, 1, 4), CancellationToken = ct }, async (source, token) =>
                {
                    // Um escopo por fonte evita compartilhar servicos scoped entre tarefas.
                    await using var isolated = scopes.CreateAsyncScope();
                    await ProcessAsync(isolated.ServiceProvider, source, run, token);
                });
                await ProcessPendingAsync(scope.ServiceProvider, ct);
                run.Status = run.SourcesFailed == 0 ? RunStatus.Success : run.SourcesFailed == run.SourcesChecked ? RunStatus.Failed : RunStatus.Partial;
            }
            catch (OperationCanceledException) { run.Status = RunStatus.Failed; run.Error = "Execução interrompida; itens persistidos serão deduplicados na retomada."; throw; }
            catch (Exception ex) { run.Status = RunStatus.Failed; run.Error = "Falha na preparação editorial: " + ex.GetType().Name; throw; }
            finally { run.FinishedAt = DateTime.UtcNow; await repository.FinishRunAsync(run, CancellationToken.None); }
        }
        finally { await lease.ExecuteAsync("SELECT RELEASE_LOCK('minhabelem_collector')"); }
    }
    private static async Task ProcessPendingAsync(IServiceProvider services, CancellationToken ct)
    {
        var ingestion = services.GetRequiredService<IIngestionRepository>();
        var discovery = services.GetRequiredService<IDiscoveryRepository>();
        var pending = (await ingestion.CandidatesAsync(new CandidateQuery { Status = CandidateStatus.Pending, PageSize = 100, OperationalOnly = true, OnlyUnimported = true }, ct))
            .Concat(await ingestion.CandidatesAsync(new CandidateQuery { Status = CandidateStatus.InReview, PageSize = 100, OperationalOnly = true }, ct));
        foreach (var candidate in pending.Take(100))
        {
            // Uma alteracao em item ja publicado fica na comparacao, sem substituir a versao publica.
            if (candidate.ContentId is not null && candidate.Status == CandidateStatus.Pending) continue;
            var source = await ingestion.SourceAsync(candidate.SourceId, ct);
            if (source is null || !source.IsOperational || string.IsNullOrWhiteSpace(source.LicenseNote) || source.Protocol == FeedProtocol.GooglePlaces) continue;
            var id = await services.GetRequiredService<IEditorialWorkflow>().ImportAsync(candidate.Id, null, ct);
            if (id is null) continue;
            var content = services.GetRequiredService<IContentRepository>();
            var draft = (await content.GetAsync(id.Value, ct))!;
            if (draft.Status != ContentStatus.InReview) continue;
            var comparisons = (await content.SearchForDuplicatesAsync(draft.Title, draft.Summary, 8, ct)).Where(x => x.Id != id).ToArray();
            var analysis = await services.GetRequiredService<IAiAnalysisService>().AnalyzeAsync(new(draft.Title, draft.Summary, draft.SourceUrl, comparisons) { TargetId = id }, ct);
            if (analysis.Classification is { } classification) { draft.Category = classification.Category; draft.Subject = classification.Subject; draft.Neighborhood = classification.Neighborhood; draft.Municipality = classification.Municipality; draft.Audiences = classification.Audiences; draft.Flags |= classification.Flags; }
            if (analysis.IsDuplicate) { draft.Flags |= ModerationFlag.PossibleDuplicate; draft.DuplicateOfId = analysis.DuplicateOfId; }
            draft.EventDetails = await discovery.EvidenceAsync(candidate.Id, ct) ?? analysis.Event;
            if (draft.EventDetails is { } ev) { draft.Kind = ContentKind.Event; draft.EventStartAt = ev.StartUtc; draft.EventEndAt = ev.EndUtc; draft.Address = ev.Location; draft.IsFree = ev.IsFree; if (ev.Uncertain) draft.Flags |= ModerationFlag.Sensitive; }
            draft.RequiresHumanApproval = true;
            if (await content.UpdateAsync(draft, null, "Descoberta e análise automática; aguarda revisão humana", ct))
                await ingestion.SetCandidateStatusAsync(candidate.Id, CandidateStatus.Approved, null, "Preparado para revisão editorial humana", ct);
        }
    }
    private async Task ProcessAsync(IServiceProvider services, IngestionSource source, IngestionRun run, CancellationToken ct)
    {
        var ingestion = services.GetRequiredService<IIngestionRepository>();
        var discovery = services.GetRequiredService<IDiscoveryRepository>();
        var profile = await discovery.ProfileAsync(source.Id, ct);
        if (profile.NextDueAt > DateTime.UtcNow) return;
        await discovery.SaveProfileAsync(profile, ct);
        lock (run) run.SourcesChecked++;
        var context = new SourceContext(source, profile, DateTime.UtcNow);
        var connector = services.GetServices<ISourceConnector>().SingleOrDefault(x => x.Supports(source.Protocol));
        var status = "Error"; var records = 0; string? error = null; string? checkpoint = null;
        var due = DateTime.UtcNow.AddMinutes(Math.Clamp(profile.FrequencyMinutes, 1, 1440));
        try
        {
            if (string.IsNullOrWhiteSpace(source.LicenseNote)) { status = "Unauthorized"; throw new InvalidOperationException("Fonte sem autorização documentada."); }
            if (!SafeUrl.Validate(source.FeedUrl).IsValid) throw new InvalidOperationException("URL não autorizada.");
            if (Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(source.FeedUrl).Query).Keys.Any(key => key.ToLowerInvariant() is "key" or "token" or "access_token" or "api_key" or "password")) throw new InvalidOperationException("Credenciais devem ser configuradas por referência segura, fora da URL.");
            var issue = connector?.ConfigurationIssue(context) ?? (connector is null ? "Conector não implementado para este formato." : null);
            if (issue is not null) { status = "AwaitingConfiguration"; throw new InvalidOperationException(issue); }
            if (source.RespectRobots && !(await services.GetRequiredService<IRobotsGate>().IsAllowedAsync(new Uri(source.FeedUrl), ct)).Allowed)
            { status = "Unauthorized"; throw new InvalidOperationException("Consulta bloqueada por robots.txt."); }
            ConnectorResult? result = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (!await discovery.ReserveRequestAsync(source.Id, Math.Clamp(profile.DailyRequestLimit, 1, 1000), ct))
                { status = "QuotaReached"; due = DateTime.UtcNow.Date.AddDays(1); throw new InvalidOperationException("Limite diário atingido."); }
                try { result = await connector!.ReadAsync(context, ct); break; }
                catch (HttpRequestException ex) when (attempt == 0 && (ex.StatusCode is null || (int)ex.StatusCode >= 500)) { await Task.Delay(TimeSpan.FromMilliseconds(300 + Random.Shared.Next(300)), ct); }
            }
            foreach (var item in result!.Items.Take(Math.Clamp(profile.MaxItems, 1, 100)))
            {
                lock (run) run.ItemsSeen++;
                if (!SafeUrl.Validate(item.Url).IsValid || string.IsNullOrWhiteSpace(item.Title)) continue;
                var stored = await discovery.StoreAsync(source, item, run.Id, ct);
                if (stored.Changed)
                {
                    records++; lock (run) run.ItemsInserted++;
                }
                else { lock (run) run.ItemsDuplicate++; }
            }
            checkpoint = result.Checkpoint;
            await ingestion.UpdateSourceRuntimeAsync(source.Id, DateTime.UtcNow, DateTime.UtcNow, null, result.ETag, result.LastModified, ct);
            status = "Active";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            lock (run) run.SourcesFailed++;
            error = ex is InvalidOperationException ? ex.Message : ex is HttpRequestException http ? "API indisponível: HTTP " + http.StatusCode : "Falha no conector: " + ex.GetType().Name;
            if (status == "Error") due = DateTime.UtcNow.AddMinutes(Math.Min(1440, profile.FrequencyMinutes * Math.Pow(2, Math.Min(source.ConsecutiveFailures + 1, 7))));
            await ingestion.UpdateSourceRuntimeAsync(source.Id, DateTime.UtcNow, null, error, null, null, ct);
            logger.LogWarning("Fonte {SourceId}: {Status}", source.Id, status);
        }
        finally { await discovery.FinishAsync(source.Id, status, due, records, checkpoint, CancellationToken.None); }
    }
}
