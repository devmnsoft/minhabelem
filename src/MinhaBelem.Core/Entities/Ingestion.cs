namespace MinhaBelem.Core.Entities;

public enum FeedProtocol { Rss, Atom, Json, Html, ICalendar, JsonLd, WebDiscovery, Instagram, Facebook, TikTok, YouTube, GooglePlaces, GovernmentApi, Custom }

/// <summary>Fonte de coleta. Só entra em operação com <see cref="Approved"/> verdadeiro.</summary>
public sealed class IngestionSource
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public FeedProtocol Protocol { get; set; } = FeedProtocol.Rss;
    public string? Topics { get; set; }
    public string? Municipality { get; set; }
    public bool Enabled { get; set; }
    public bool Approved { get; set; }
    public bool RespectRobots { get; set; } = true;
    public string? LicenseNote { get; set; }
    public DateTime? LastCheckedAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public string? LastError { get; set; }
    public int ConsecutiveFailures { get; set; }
    public string? ETag { get; set; }
    public DateTime? LastModified { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool IsOperational => Enabled && Approved;
}

public enum CandidateStatus { Pending, InReview, Approved, Rejected, Duplicate }

/// <summary>Matéria bruta coletada, aguardando curadoria humana e/ou IA.</summary>
public sealed class IngestionCandidate
{
    public long Id { get; set; }
    public long SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public string? ExternalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public byte[]? SourceUrlHash { get; set; }
    public string? AuthorName { get; set; }
    public DateTime PublishedAt { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageCredit { get; set; }
    public CandidateStatus Status { get; set; } = CandidateStatus.Pending;
    public long? DuplicateOfId { get; set; }
    public long? ContentId { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum RunStatus { Running, Success, Partial, Failed }

/// <summary>Registro de uma execução do coletor. É o histórico exigido pela especificação.</summary>
public sealed class IngestionRun
{
    public long Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public RunStatus Status { get; set; }
    public int SourcesChecked { get; set; }
    public int SourcesFailed { get; set; }
    public int ItemsSeen { get; set; }
    public int ItemsInserted { get; set; }
    public int ItemsDuplicate { get; set; }
    public string? Error { get; set; }
}
