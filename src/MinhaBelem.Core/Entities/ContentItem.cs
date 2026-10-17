using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Core.Entities;

public enum ContentKind { News, Event, Service, Alert, Guide }

public enum ContentStatus { Draft, InReview, Published, Archived, Rejected, Cancelled, Postponed, UncertainDate, AwaitingOrganizer, Completed }

public enum VerificationStatus { Pending, Verified, Outdated }

public enum ModerationFlag
{
    None = 0,
    Offensive = 1,
    Sensitive = 2,
    Denunciation = 4,
    CriticalAlert = 8,
    Outdated = 16,
    PossibleDuplicate = 32
}

/// <summary>Item publicado ou em produção. Tudo que aparece no portal nasce aqui.</summary>
public sealed class ContentItem
{
    public long Id { get; set; }
    public ContentKind Kind { get; set; } = ContentKind.News;
    public ContentCategory Category { get; set; } = ContentCategory.Cultura;
    public string? Subject { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Body { get; set; }

    public string? LocalizedTitle { get; set; }
    public string? LocalizedSummary { get; set; }
    public string? LocalizedBody { get; set; }
    public string? CulturalNotes { get; set; }

    public string DisplayTitle => !string.IsNullOrWhiteSpace(LocalizedTitle) ? LocalizedTitle : Title;
    public string DisplaySummary => !string.IsNullOrWhiteSpace(LocalizedSummary) ? LocalizedSummary : Summary;
    public string DisplayBody => !string.IsNullOrWhiteSpace(LocalizedBody) ? LocalizedBody : (Body ?? string.Empty);


    public string? Neighborhood { get; set; }
    public string? Municipality { get; set; }
    public string State { get; set; } = BelemGeography.State;
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public long? SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public byte[]? SourceUrlHash { get; set; }
    public string? AuthorName { get; set; }
    public DateTime? SourcePublishedAt { get; set; }

    public string? ImageUrl { get; set; }
    public string? ImageCredit { get; set; }
    public string? ImageLicense { get; set; }

    public DateTime? EventStartAt { get; set; }
    public DateTime? EventEndAt { get; set; }
    public decimal? TicketPrice { get; set; }
    public string? TicketPriceText { get; set; }
    public bool IsFree { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
    public ModerationFlag Flags { get; set; } = ModerationFlag.None;
    public long? DuplicateOfId { get; set; }
    public bool RequiresHumanApproval { get; set; } = true;

    public DateTime PublishedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime LastVerifiedAt { get; set; }
    public long? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public long? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Revision { get; set; } = 1;
    public Services.EventEvidence? EventDetails { get; set; }

    public IReadOnlyList<Audience> Audiences { get; set; } = Array.Empty<Audience>();
    public IReadOnlyList<AccessibilityFeature> Accessibility { get; set; } = Array.Empty<AccessibilityFeature>();

    public bool IsExpired(DateTime now) => ExpiresAt.HasValue && ExpiresAt.Value <= now;

    public bool IsVisibleToPublic(DateTime now) =>
        (Status is ContentStatus.Published or ContentStatus.Cancelled) && !IsExpired(now) && VerificationStatus != VerificationStatus.Outdated;
}

public sealed class ContentRevision
{
    public long Id { get; set; }
    public long ContentId { get; set; }
    public int Revision { get; set; }
    public string? ChangeSummary { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public long? ChangedById { get; set; }
    public string? ChangedByName { get; set; }
    public DateTime ChangedAt { get; set; }
}
