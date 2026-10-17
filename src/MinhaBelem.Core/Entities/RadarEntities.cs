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
    public RadarHeartbeat? Heartbeat { get; set; }
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

public enum WorkerStatus { Active, Delayed, Off, Error, Running, Unknown }

public sealed class RadarHeartbeat
{
    public long Id { get; set; }
    public string WorkerName { get; set; } = "RadarWorker";
    public DateTime LastHeartbeatAt { get; set; }
    public DateTime? LastCycleStartedAt { get; set; }
    public DateTime? LastCycleFinishedAt { get; set; }
    public string LastCycleStatus { get; set; } = "idle";
    public string? LastCycleSummary { get; set; }
    public string? LastError { get; set; }
    public bool IsRunning { get; set; }
    public int ConsecutiveErrors { get; set; }
    public int TotalCyclesExecuted { get; set; }
    public DateTime? NextCycleDueAt { get; set; }
    public WorkerStatus Status => CalculateStatus();

    public WorkerStatus CalculateStatus()
    {
        if (LastHeartbeatAt == default) return WorkerStatus.Unknown;
        var diff = DateTime.UtcNow - LastHeartbeatAt;
        if (IsRunning) return WorkerStatus.Running;
        if (ConsecutiveErrors >= 3 || LastCycleStatus.Equals("failed", StringComparison.OrdinalIgnoreCase)) return WorkerStatus.Error;
        if (diff > TimeSpan.FromMinutes(25)) return WorkerStatus.Off;
        if (diff > TimeSpan.FromMinutes(12)) return WorkerStatus.Delayed;
        return WorkerStatus.Active;
    }

    public string StatusBadgeClass => CalculateStatus() switch
    {
        WorkerStatus.Active => "badge-success",
        WorkerStatus.Running => "badge-info",
        WorkerStatus.Delayed => "badge-warning",
        WorkerStatus.Error => "badge-danger",
        WorkerStatus.Off => "badge-secondary",
        _ => "badge-secondary"
    };

    public string StatusBadgeColor => CalculateStatus() switch
    {
        WorkerStatus.Active => "rgba(46,125,50,0.15)",
        WorkerStatus.Running => "rgba(2,132,199,0.15)",
        WorkerStatus.Delayed => "rgba(217,119,6,0.15)",
        WorkerStatus.Error => "rgba(220,38,38,0.15)",
        WorkerStatus.Off => "rgba(100,116,139,0.15)",
        _ => "#f1f5f9"
    };

    public string StatusText => CalculateStatus() switch
    {
        WorkerStatus.Active => "Ativo (No Horário)",
        WorkerStatus.Running => "Em Execução...",
        WorkerStatus.Delayed => "Atrasado (>12m)",
        WorkerStatus.Error => "Falha / Alerta",
        WorkerStatus.Off => "Desligado (>25m)",
        _ => "Sem Informação"
    };
}

public sealed class SocialPost
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public string? ExternalPostId { get; set; }
    public string PostUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public string? AuthorHandle { get; set; }
    public string Platform { get; set; } = "Instagram";
    public string MediaType { get; set; } = "post";
    public string? EmbedHtml { get; set; }
    public DateTime? PublishedAt { get; set; }
    public long? ContentId { get; set; }
    public long? LinkedContentId => ContentId;
    public string? CaptionSnippet => !string.IsNullOrWhiteSpace(Caption) ? (Caption.Length > 80 ? Caption.Substring(0, 80) + "..." : Caption) : (!string.IsNullOrWhiteSpace(Title) ? Title : "Publicação oficial");
    public bool IsSponsored { get; set; }
    public string? SponsorName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class RadarOperationalSource
{
    public IngestionSource Source { get; set; } = new();
    public long Id => Source.Id;
    public string SourceName => Source.Name;
    public string Url => Source.FeedUrl;
    public string Category => Source.Topics ?? "geral";
    public string Municipality => Source.Municipality ?? "Belém";
    public bool Enabled => Source.Enabled;
    public bool Approved => Source.Approved;
    public DateTime? LastCheckedAt => Source.LastCheckedAt;
    public DateTime? LastSuccessAt => Source.LastSuccessAt;
    public int AvgResponseTimeMs { get; set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Source.FeedUrl);
    public bool IsAuthorized => !string.IsNullOrWhiteSpace(Source.LicenseNote) && Source.Approved;
    public bool IsConsulted => Source.LastCheckedAt.HasValue;
    public bool IsOperational => Source.IsOperational;
    public bool IsOutdated => (Source.LastSuccessAt.HasValue && (DateTime.UtcNow - Source.LastSuccessAt.Value).TotalDays > 7) || Source.ConsecutiveFailures >= 3;
    public string? LastError => Source.LastError;
    public int ConsecutiveFailures => Source.ConsecutiveFailures;
}
