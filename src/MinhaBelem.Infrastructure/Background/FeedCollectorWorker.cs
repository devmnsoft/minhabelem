using System.Diagnostics;
using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinhaBelem.Infrastructure.Background;

public sealed class CollectorOptions
{
    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 10;
    public int MaxItemsPerFeed { get; set; } = 40;
    public int MaxConcurrentSources { get; set; } = 4;
}

public sealed record FeedReadResult(IReadOnlyList<FeedItem> Items, string? ETag, DateTime? LastModified);

public interface IFeedClient
{
    Task<FeedReadResult> ReadAsync(Uri feedUri, string? etag, DateTime? lastModified, CancellationToken ct);
}

/// <summary>Cliente HTTP de feed com ETag/Last-Modified e proteção SSRF.</summary>
public sealed class HttpFeedClient(HttpClient http) : IFeedClient
{
    public async Task<FeedReadResult> ReadAsync(Uri feedUri, string? etag, DateTime? lastModified, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, feedUri);
        request.Headers.UserAgent.ParseAdd("MinhaBelemBot/1.0 (+https://minhabelem.com.br/coleta)");
        request.Headers.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml;q=0.9, */*;q=0.5");
        if (!string.IsNullOrWhiteSpace(etag)) request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        if (lastModified is not null) request.Headers.IfModifiedSince = lastModified.Value;

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var newEtag = response.Headers.ETag?.Tag;
        var newLastModified = response.Content.Headers.LastModified?.UtcDateTime;

        if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
            return new FeedReadResult(Array.Empty<FeedItem>(), newEtag, newLastModified);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > 4_000_000)
            throw new InvalidOperationException("Feed excede o tamanho máximo permitido.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings
        {
            DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 4_000_000,
            MaxCharactersFromEntities = 100_000,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            CloseInput = false
        });

        System.ServiceModel.Syndication.SyndicationFeed? feed;
        try { feed = System.ServiceModel.Syndication.SyndicationFeed.Load(reader); }
        catch (System.Xml.XmlException) { return new FeedReadResult(Array.Empty<FeedItem>(), newEtag, newLastModified); }

        if (feed is null) return new FeedReadResult(Array.Empty<FeedItem>(), newEtag, newLastModified);

        var items = feed.Items.Select(item =>
        {
            var title = HtmlText.Clean(item.Title?.Text, 300);
            var link = item.Links.Select(l => l.Uri).FirstOrDefault(u => u is not null && u.Scheme == Uri.UriSchemeHttps);
            var summary = HtmlText.ToPlainText(item.Summary?.Text ?? (item.Content as System.ServiceModel.Syndication.TextSyndicationContent)?.Text ?? string.Empty, 1500);
            return new FeedItem(
                title,
                summary,
                link?.AbsoluteUri,
                HtmlText.Clean(item.Authors?.FirstOrDefault()?.Name, 240),
                item.PublishDate != default ? item.PublishDate.UtcDateTime : null,
                null);
        }).Where(i => i.Title.Length > 0).ToArray();

        return new FeedReadResult(items, newEtag, newLastModified);
    }
}

/// <summary>
/// Coletor automático. Executa a cada 10 minutos (configurável), por fonte aprovada,
/// respeitando robots.txt, licenças registradas e a proteção SSRF. Grava histórico em
/// <c>mb_ingestion_runs</c> e candidatos deduplicados em <c>mb_ingestion_candidates</c>.
/// </summary>
public sealed class FeedCollectorWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<CollectorOptions> options,
    ILogger<FeedCollectorWorker> logger) : BackgroundService
{
    private readonly CollectorOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Coletor automático desligado (Collector:Enabled=false).");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Clamp(_options.IntervalMinutes, 10, 240));
        logger.LogInformation("Coletor automático ativo a cada {Interval}.", interval);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await CollectOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha inesperada na execução do coletor");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    public async Task CollectOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;
        var ingestion = provider.GetRequiredService<IIngestionRepository>();
        var sources = await ingestion.SourcesAsync(onlyOperational: true, ct);

        var run = new IngestionRun { StartedAt = DateTime.UtcNow, FinishedAt = DateTime.UtcNow, Status = RunStatus.Running };
        var runId = await ingestion.StartRunAsync(ct);
        run.Id = runId;

        var semaphore = new SemaphoreSlim(Math.Clamp(_options.MaxConcurrentSources, 1, 8));
        var stopwatch = Stopwatch.StartNew();

        var tasks = sources.Select(async source =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                await ProcessSourceAsync(ingestion, provider, source, run, ct);
            }
            catch (Exception ex)
            {
                lock (run) { run.SourcesFailed++; }
                run.Error = HtmlText.Truncate($"{source.Name}: {ex.Message}", 900);
                logger.LogWarning(ex, "Falha ao coletar a fonte {Source}", source.Name);
                await SafeUpdateSourceAsync(ingestion, source, false, ex.Message, null, null, ct);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        run.FinishedAt = DateTime.UtcNow;
        run.Status = run.SourcesFailed switch
        {
            0 => RunStatus.Success,
            _ when run.SourcesChecked > 0 => RunStatus.Partial,
            _ => RunStatus.Failed
        };
        await ingestion.FinishRunAsync(run, ct);
        logger.LogInformation(
            "Coleta concluída em {Elapsed}ms: {Checked} fontes, {Seen} itens vistos, {Inserted} novos, {Duplicates} duplicados, {Failed} falhas.",
            stopwatch.ElapsedMilliseconds, run.SourcesChecked, run.ItemsSeen, run.ItemsInserted, run.ItemsDuplicate, run.SourcesFailed);
    }

    private async Task ProcessSourceAsync(IIngestionRepository ingestion, IServiceProvider provider, IngestionSource source, IngestionRun run, CancellationToken ct)
    {
        lock (run) { run.SourcesChecked++; }

        var validation = SafeUrl.Validate(source.FeedUrl);
        if (!validation.IsValid || validation.Uri is null)
        {
            lock (run) { run.SourcesFailed++; }
            await SafeUpdateSourceAsync(ingestion, source, false, validation.Reason, null, null, ct);
            return;
        }

        if (source.RespectRobots)
        {
            var robots = provider.GetRequiredService<IRobotsGate>();
            var decision = await robots.IsAllowedAsync(validation.Uri, ct);
            if (!decision.Allowed)
            {
                await SafeUpdateSourceAsync(ingestion, source, false, decision.Reason, null, null, ct);
                logger.LogInformation("Fonte {Source} ignorada: {Reason}", source.Name, decision.Reason);
                return;
            }
        }

        var client = provider.GetRequiredService<IFeedClient>();
        var read = await client.ReadAsync(validation.Uri, source.ETag, source.LastModified, ct);
        var items = read.Items;

        if (items.Count == 0)
        {
            await SafeUpdateSourceAsync(ingestion, source, true, null, read.ETag, read.LastModified, ct);
            return;
        }

        var inserted = 0;
        var duplicates = 0;

        foreach (var item in items.Take(Math.Clamp(_options.MaxItemsPerFeed, 1, 200)))
        {
            ct.ThrowIfCancellationRequested();
            lock (run) { run.ItemsSeen++; }

            if (SafeUrl.Validate(item.Link).Uri is null) continue;
            if (string.IsNullOrWhiteSpace(item.Title)) continue;

            var duplicate = await ingestion.SearchForDuplicatesAsync(item.Title, item.Summary, item.Link!, 5, ct);
            var match = duplicate.Count == 0 ? null : duplicate[0];

            var candidate = new IngestionCandidate
            {
                SourceId = source.Id,
                SourceName = source.Name,
                RunId = run.Id,
                Title = HtmlText.Truncate(item.Title, 295),
                Summary = HtmlText.Truncate(item.Summary, 1490),
                SourceUrl = item.Link!,
                AuthorName = item.Author,
                PublishedAt = item.PublishedAt ?? DateTime.UtcNow,
                Status = match is null ? CandidateStatus.Pending : CandidateStatus.Duplicate,
                DuplicateOfId = match?.Id
            };

            var stored = await ingestion.InsertCandidateAsync(candidate, ct);
            if (stored is null) continue;

            if (match is not null)
            {
                duplicates++;
                lock (run) { run.ItemsDuplicate++; }
            }
            else if (stored.Id == candidate.Id)
            {
                inserted++;
                lock (run) { run.ItemsInserted++; }
            }
        }

        await SafeUpdateSourceAsync(ingestion, source, true, null, read.ETag, read.LastModified, ct);
        logger.LogInformation("Fonte {Source}: {Seen} itens, {Inserted} novos, {Duplicates} duplicados.", source.Name, items.Count, inserted, duplicates);
    }

    private static async Task SafeUpdateSourceAsync(IIngestionRepository ingestion, IngestionSource source, bool success, string? error, string? etag, DateTime? lastModified, CancellationToken ct)
    {
        try
        {
            await ingestion.UpdateSourceRuntimeAsync(source.Id, DateTime.UtcNow, success ? DateTime.UtcNow : null, error, etag, lastModified, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Falha ao registrar o status não deve interromper a coleta.
        }
    }
}