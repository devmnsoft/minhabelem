namespace MinhaBelem.Core.Entities;

public enum SubmissionStatus { Pending, InReview, Approved, Rejected }

public sealed class CommunitySubmission
{
    public long Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Contact { get; set; }
    public string Message { get; set; } = string.Empty;
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Pending;
    public string? ReviewNotes { get; set; }
    public long? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AdvertiserCampaign
{
    public long Id { get; set; }
    public string AdvertiserName { get; set; } = string.Empty;
    public string DestinationUrl { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Label { get; set; } = "Publicidade";
    public string? Headline { get; set; }
    public bool Active { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string? AdvertiserUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AuditEntry
{
    public long Id { get; set; }
    public long? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public string? DetailsJson { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Usuário do painel com perfil e permissões agregadas.</summary>
public sealed class UserProfile
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool MfaEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}

public sealed class DashboardSnapshot
{
    public int PublishedContent { get; set; }
    public int ContentInReview { get; set; }
    public int ContentDrafts { get; set; }
    public int PendingCandidates { get; set; }
    public int DuplicateCandidates { get; set; }
    public int ActiveSources { get; set; }
    public int FailingSources { get; set; }
    public int PublishedPlaces { get; set; }
    public int PendingSubmissions { get; set; }
    public int ActiveAdvertisers { get; set; }
    public int ActiveUsers { get; set; }
    public int AiRunsLast24h { get; set; }
    public int AiFailuresLast24h { get; set; }
    public decimal AiCostUsdLast30d { get; set; }
    public IReadOnlyList<AuditEntry> RecentAudit { get; set; } = Array.Empty<AuditEntry>();
    public IReadOnlyList<DailyPoint> PublishedByDay { get; set; } = Array.Empty<DailyPoint>();
    public IReadOnlyList<CategoryPoint> ByCategory { get; set; } = Array.Empty<CategoryPoint>();
}

public sealed record DailyPoint(DateOnly Day, int Count);
public sealed record CategoryPoint(string Category, int Count);