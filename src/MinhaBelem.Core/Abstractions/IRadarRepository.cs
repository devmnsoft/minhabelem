using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Core.Abstractions;

public interface IRadarRepository
{
    Task<RadarDashboardStats> GetDashboardStatsAsync(CancellationToken ct);
    Task<IReadOnlyList<DiscoveredSource>> DiscoveredSourcesAsync(DiscoveredSourceQuery query, CancellationToken ct);
    Task<int> CountDiscoveredSourcesAsync(DiscoveredSourceQuery query, CancellationToken ct);
    Task<long> InsertDiscoveredSourceAsync(DiscoveredSource source, CancellationToken ct);
    Task<bool> EvaluateDiscoveredSourceAsync(long id, SourceEvaluationStatus status, string? notes, long? evaluatedById, CancellationToken ct);
    Task<IReadOnlyList<CrawlHistoryEntry>> RecentCrawlHistoryAsync(int limit, CancellationToken ct);
    Task<long> RecordCrawlHistoryAsync(CrawlHistoryEntry entry, CancellationToken ct);
    Task<IReadOnlyList<SocialProfileRegistry>> SocialProfilesAsync(bool onlyActive, CancellationToken ct);
    Task<long> UpsertSocialProfileAsync(SocialProfileRegistry profile, CancellationToken ct);
    Task<bool> ToggleSocialProfileAsync(long id, bool isActive, CancellationToken ct);
    Task<CrawlHistoryEntry?> GetLastCrawlForUrlAsync(string url, CancellationToken ct);
    Task UpdateHeartbeatAsync(string workerName, string status, string? summary, string? error, bool isRunning, CancellationToken ct);
    Task<RadarHeartbeat?> GetHeartbeatAsync(string workerName, CancellationToken ct);
    Task ScheduleAsync(DateTime? nextCycleUtc, CancellationToken ct);
    Task<long> StartCycleAsync(CancellationToken ct);
    Task FinishCycleAsync(RadarCycle cycle, CancellationToken ct);
    Task<IReadOnlyList<SocialPost>> SocialPostsAsync(int limit, long? profileId, CancellationToken ct);
    Task<long> InsertSocialPostAsync(SocialPost post, CancellationToken ct);
    Task<IReadOnlyList<RadarOperationalSource>> OperationalSourcesAsync(CancellationToken ct);
    Task<bool> ToggleSourceStateAsync(long sourceId, bool enabled, CancellationToken ct);
}

public interface IHtmlContentExtractor
{
    ExtractedHtmlContent Extract(string html, Uri baseUri);
}

public interface IEventStructuredDataExtractor
{
    IReadOnlyList<DiscoveredItem> ExtractEvents(string html, string url);
    EventEvidence? ExtractEventFromText(string text, string url);
}

public interface ISitemapDiscoveryService
{
    Task<IReadOnlyList<string>> DiscoverUrlsAsync(string sitemapUrlOrDomain, CancellationToken ct);
}

public interface IPublicRssDiscoveryService
{
    Task<IReadOnlyList<DiscoveredItem>> DiscoverFeedAsync(string feedUrlOrHtml, CancellationToken ct);
    Task<IReadOnlyList<string>> DiscoverFeedLinksInHtmlAsync(string html, Uri baseUri, CancellationToken ct);
}

public interface IEventPosterAnalyzer
{
    Task<EventEvidence?> AnalyzePosterAsync(string imageUrl, string contextTitle, CancellationToken ct);
}

public interface ISourceChangeDetector
{
    bool HasChanged(string newContent, string? previousContentHash, string? newEtag, string? previousEtag, DateTime? newModified, DateTime? previousModified);
    string ComputeContentHash(string content);
}

public interface IContentDeduplicationService
{
    Task<DuplicateMatch?> DetectDuplicateAsync(string title, string summary, string url, DateTime? eventStart, CancellationToken ct);
}

public interface IPublicWebCrawler
{
    Task<CrawlResult> CrawlAsync(IngestionSource source, CrawlerOptions? options, CancellationToken ct);
    Task<CrawlResult> CrawlTargetUrlAsync(string targetUrl, CrawlerOptions? options, CancellationToken ct);
}
