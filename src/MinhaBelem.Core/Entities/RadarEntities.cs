namespace MinhaBelem.Core.Entities;

public enum SourceEvaluationStatus { Pending, Approved, Rejected }

public sealed class DiscoveredSource
{
    public long Id { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public byte[]? UrlHash { get; set; }
    public string Category { get; set; } = "geral";
    public string Municipality { get; set; } = "Belém";
    public string? DiscoveredFromUrl { get; set; }
    public SourceEvaluationStatus EvaluationStatus { get; set; } = SourceEvaluationStatus.Pending;
    public string? EvaluationNotes { get; set; }
    public string FeedProtocolCandidate { get; set; } = "html";
    public decimal ConfidenceScore { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public DateTime? EvaluatedAt { get; set; }
    public long? EvaluatedById { get; set; }
}

public sealed class CrawlHistoryEntry
{
    public long Id { get; set; }
    public long? SourceId { get; set; }
    public string Url { get; set; } = string.Empty;
    public byte[]? UrlHash { get; set; }
    public int StatusCode { get; set; }
    public string? ETag { get; set; }
    public DateTime? LastModified { get; set; }
    public string? ContentHash { get; set; }
    public int DiscoveredItemsCount { get; set; }
    public int EventsIdentifiedCount { get; set; }
    public int Depth { get; set; }
    public int DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CrawledAt { get; set; }
}

public enum SocialPlatform { Instagram, Facebook, TikTok, YouTube }

public sealed class SocialProfileRegistry
{
    public long Id { get; set; }
    public SocialPlatform Platform { get; set; }
    public string ProfileHandle { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
    public string? OfficialEmbedUrl { get; set; }
    public string Category { get; set; } = "cultura";
    public string Municipality { get; set; } = "Belém";
    public string LicenseNotes { get; set; } = "Conteúdo público oficial para divulgação cultural";
    public bool IsActive { get; set; } = true;
    public DateTime? LastCheckedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CrawlerOptions
{
    public int MaxDepth { get; set; } = 2;
    public int MaxPagesPerHost { get; set; } = 20;
    public int DelayBetweenRequestsMs { get; set; } = 250;
    public string UserAgent { get; set; } = "MinhaBelemRadar/1.0 (+https://minhabelem.com.br/radar)";
    public int TimeoutSeconds { get; set; } = 15;
    public int MaxContentLengthBytes { get; set; } = 2_000_000;
}

public sealed class CrawlResult
{
    public int UrlsCrawled { get; set; }
    public int ItemsDiscovered { get; set; }
    public int EventsIdentified { get; set; }
    public int DiscoveredSourcesCount { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; } = new();
}

public sealed class ExtractedHtmlContent
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? CanonicalUrl { get; set; }
    public string? Author { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? LeadImageUrl { get; set; }
    public string? LeadImageCredit { get; set; }
    public string RawCleanText { get; set; } = string.Empty;
    public IReadOnlyList<string> OutboundLinks { get; set; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> MetaTags { get; set; } = new Dictionary<string, string>();
    public IReadOnlyList<string> BelemKeywordsFound { get; set; } = Array.Empty<string>();
    public bool IsBelemRelevant => BelemKeywordsFound.Count > 0;
}

public sealed class RadarDashboardStats
{
    public int TotalSources { get; set; }
    public int ActiveSources { get; set; }
    public int DiscoveredItemsCount { get; set; }
    public int UpcomingEventsCount { get; set; }
    public int PendingReviewCount { get; set; }
    public int DuplicateItemsCount { get; set; }
    public int CrawlErrorsCount { get; set; }
    public long AiTokensConsumed { get; set; }
    public DateTime? LastCrawlAt { get; set; }
    public DateTime? NextCrawlDueAt { get; set; }
    public int SocialProfilesCount { get; set; }
}

public sealed class DiscoveredSourceQuery
{
    public SourceEvaluationStatus? Status { get; set; }
    public string? Category { get; set; }
    public string? Municipality { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
