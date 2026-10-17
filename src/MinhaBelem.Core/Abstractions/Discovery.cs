using MinhaBelem.Core.Entities;

namespace MinhaBelem.Core.Services;

public sealed class SourceProfile
{
    public long SourceId { get; set; }
    public string OwnerName { get; set; } = "";
    public string Organization { get; set; } = "";
    public string Region { get; set; } = "Pará";
    public string Keywords { get; set; } = "";
    public int FrequencyMinutes { get; set; } = 10;
    public int DailyRequestLimit { get; set; } = 24;
    public int MaxItems { get; set; } = 20;
    public string? CredentialReference { get; set; }
    public string? Checkpoint { get; set; }
    public string Status { get; set; } = "AwaitingConfiguration";
    public DateTime? NextDueAt { get; set; }
    public long RecordsFound { get; set; }
}
public sealed record EventEvidence(DateTime? StartUtc, DateTime? EndUtc, string? Location, string? Organizer, string? Status, string? Price, bool IsFree, string? AgeRating, string? Participants, string? EvidenceUrl, bool Uncertain = false)
{
    public string? PublicUpdateNote { get; init; }
    public string? Availability { get; init; }
}
public sealed record DiscoveredItem(string Title, string Summary, string Url, string? Author, DateTime? PostedAt, EventEvidence? Event = null, string? ExternalId = null)
{
    public string? MediaUrl { get; init; }
    public string? MediaCredit { get; init; }
    public string? MediaLicense { get; init; }
}
public sealed record ConnectorResult(IReadOnlyList<DiscoveredItem> Items, string? ETag = null, DateTime? LastModified = null, string? Checkpoint = null);
public sealed record SourceContext(IngestionSource Source, SourceProfile Profile, DateTime NowUtc);
public interface ISourceConnector
{
    bool Supports(FeedProtocol protocol);
    string? ConfigurationIssue(SourceContext context);
    Task<ConnectorResult> ReadAsync(SourceContext context, CancellationToken ct);
}
public interface IRssAtomConnector : ISourceConnector { }
public interface IWebDiscoveryConnector : ISourceConnector { }
public interface IInstagramConnector : ISourceConnector { }
public interface IFacebookConnector : ISourceConnector { }
public interface ITikTokConnector : ISourceConnector { }
public interface IYouTubeConnector : ISourceConnector { }
public interface IGooglePlacesConnector : ISourceConnector { }
public interface ICalendarConnector : ISourceConnector { }
public interface IGovernmentDataConnector : ISourceConnector { }
public interface IDiscoveryRepository
{
    Task<SourceProfile> ProfileAsync(long sourceId, CancellationToken ct);
    Task SaveProfileAsync(SourceProfile profile, CancellationToken ct);
    Task<bool> ReserveRequestAsync(long sourceId, int dailyLimit, CancellationToken ct);
    Task FinishAsync(long sourceId, string status, DateTime nextDue, int records, string? checkpoint, CancellationToken ct);
    Task<(long Id, bool Changed)> StoreAsync(IngestionSource source, DiscoveredItem item, long runId, CancellationToken ct);
    Task<EventEvidence?> EvidenceAsync(long candidateId, CancellationToken ct);
}
public interface ISourceCollection { Task CollectOnceAsync(CancellationToken ct); }
public interface IMediaAnalysis
{
    bool IsConfigured { get; }
    Task<MediaAnalysisResult> AnalyzeAsync(long candidateId, long actorId, CancellationToken ct);
}
public sealed record MediaAnalysisResult(string Text, string ThumbnailDataUrl, string AccessibleDescription, string Credit, string License);
