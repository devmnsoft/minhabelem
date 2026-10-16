using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Taxonomy;

namespace MinhaBelem.Core.Abstractions;

public interface IContentRepository
{
    Task<IReadOnlyList<ContentItem>> EditorialAsync(ContentQuery query, CancellationToken ct);
    Task<IReadOnlyList<ContentItem>> SearchAsync(ContentQuery query, CancellationToken ct);
    Task<int> CountAsync(ContentQuery query, CancellationToken ct);
    Task<ContentItem?> GetAsync(long id, CancellationToken ct);
    Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken ct);
    Task<long> InsertAsync(ContentItem item, long? actorId, string changeSummary, CancellationToken ct);
    Task<bool> UpdateAsync(ContentItem item, long? actorId, string changeSummary, CancellationToken ct);
    Task<bool> SetStatusAsync(long id, ContentStatus status, long actorId, CancellationToken ct);
    Task<bool> DeleteAsync(long id, long actorId, CancellationToken ct);
    Task<IReadOnlyList<ContentRevision>> RevisionsAsync(long contentId, CancellationToken ct);
    Task<IReadOnlyDictionary<long, string>> FingerprintsAsync(IEnumerable<long> ids, CancellationToken ct);
    Task<IReadOnlyList<ContentItem>> SearchForDuplicatesAsync(string title, string summary, int limit, CancellationToken ct);
    Task<int> ExpireOldContentAsync(DateTime now, CancellationToken ct);
}

public interface IEditorialWorkflow
{
    Task<long?> ImportAsync(long candidateId, long? actorId, CancellationToken ct);
}

public sealed class ContentQuery
{
    public ContentCategory? Category { get; set; }
    public string? Subject { get; set; }
    public IReadOnlyList<string>? Subjects { get; set; }
    public string? Search { get; set; }
    public string? Neighborhood { get; set; }
    public string? Municipality { get; set; }
    public Audience? Audience { get; set; }
    public AccessibilityFeature? Accessibility { get; set; }
    public bool? FreeOnly { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public bool OnlyUpcomingEvents { get; set; }
    public DateTime? StartsFromUtc { get; set; }
    public DateTime? StartsBeforeUtc { get; set; }
    public int? AgeAtMost { get; set; }
    public ContentStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public ContentSort Sort { get; set; } = ContentSort.Recent;
    public string? Culture { get; set; }
}

public enum ContentSort { Recent, Upcoming, Oldest }

public interface IIngestionRepository
{
    Task<IReadOnlyList<IngestionSource>> SourcesAsync(bool onlyOperational, CancellationToken ct);
    Task<IngestionSource?> SourceAsync(long id, CancellationToken ct);
    Task<long> UpsertSourceAsync(IngestionSource source, CancellationToken ct);
    Task<bool> UpdateSourceRuntimeAsync(long id, DateTime checkedAt, DateTime? successAt, string? error, string? etag, DateTime? lastModified, CancellationToken ct);
    Task<long> StartRunAsync(CancellationToken ct);
    Task FinishRunAsync(IngestionRun run, CancellationToken ct);

    Task<IngestionCandidate?> InsertCandidateAsync(IngestionCandidate candidate, CancellationToken ct);
    Task<IReadOnlyList<IngestionCandidate>> CandidatesAsync(CandidateQuery query, CancellationToken ct);
    Task<int> CountCandidatesAsync(CandidateQuery query, CancellationToken ct);
    Task<IngestionCandidate?> CandidateAsync(long id, CancellationToken ct);
    Task<bool> SetCandidateStatusAsync(long id, CandidateStatus status, long? reviewerId, string? notes, CancellationToken ct);
    Task<IReadOnlyList<IngestionCandidate>> SearchForDuplicatesAsync(string title, string summary, string sourceUrl, int limit, CancellationToken ct);
}

public sealed class CandidateQuery
{
    public bool OperationalOnly { get; set; }
    public bool OnlyUnimported { get; set; }
    public CandidateStatus? Status { get; set; }
    public long? SourceId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public interface IPlaceRepository
{
    Task<Place?> GetByGoogleIdAsync(string googlePlaceId, CancellationToken ct);
    Task<Place?> GetAsync(long id, CancellationToken ct);
    Task<Place?> GetBySlugAsync(string slug, CancellationToken ct);
    Task<IReadOnlyList<Place>> SearchAsync(PlaceQuery query, CancellationToken ct);
    Task<long> UpsertAsync(Place place, CancellationToken ct);
    Task<int> PurgeExpiredAsync(DateTime now, CancellationToken ct);
    Task<bool> SetPublishedAsync(long id, bool published, long actorId, CancellationToken ct);
    Task<bool> UpdateEditorialAsync(long id, string? note, string? noteSource, string? neighborhoods, CancellationToken ct);
}

public sealed class PlaceQuery
{
    public string? Category { get; set; }
    public string? Neighborhood { get; set; }
    public string? Municipality { get; set; }
    public Audience? Audience { get; set; }
    public AccessibilityFeature? Accessibility { get; set; }
    public bool? FreeOnly { get; set; }
    public string? Search { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? RadiusKm { get; set; }
    public bool PublishedOnly { get; set; } = true;
    public PlaceSort Sort { get; set; } = PlaceSort.Relevance;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
    public string? Culture { get; set; }
}

public interface IAuditRepository
{
    Task WriteAsync(AuditEntry entry, CancellationToken ct);
    Task<IReadOnlyList<AuditEntry>> RecentAsync(int take, CancellationToken ct);
}

public interface IAiRunRepository
{
    Task<long> SaveAsync(AiRun run, CancellationToken ct);
    Task<IReadOnlyList<AiRun>> RecentAsync(int take, CancellationToken ct);
    Task<decimal> CostSinceAsync(DateTime since, CancellationToken ct);
    Task<(int Total, int Failed)> StatsSinceAsync(DateTime since, CancellationToken ct);
}

public interface ICommunityRepository
{
    Task<long> InsertAsync(CommunitySubmission submission, CancellationToken ct);
    Task<IReadOnlyList<CommunitySubmission>> SearchAsync(SubmissionStatus? status, int take, CancellationToken ct);
    Task<bool> SetStatusAsync(long id, SubmissionStatus status, long reviewerId, string? notes, CancellationToken ct);
}

public interface IAdvertiserRepository
{
    Task<IReadOnlyList<AdvertiserCampaign>> ActiveAsync(DateTime now, int take, CancellationToken ct);
    Task<IReadOnlyList<AdvertiserCampaign>> AllAsync(CancellationToken ct);
    Task<long> UpsertAsync(AdvertiserCampaign campaign, CancellationToken ct);
    Task<bool> SetActiveAsync(long id, bool active, CancellationToken ct);
}

public interface IDashboardRepository
{
    Task<DashboardSnapshot> BuildAsync(CancellationToken ct);
}

public sealed record TranslationStats(
    int TotalContent,
    int TotalPlaces,
    int TotalTranslations,
    int PendingReviews,
    int ApprovedTranslations,
    IReadOnlyDictionary<string, int> CountByLanguage);

public interface ITranslationRepository
{
    Task<IReadOnlyList<Language>> GetLanguagesAsync(bool onlyEnabled, CancellationToken ct);
    Task<Language?> GetLanguageAsync(string code, CancellationToken ct);
    Task<bool> UpdateLanguageAsync(Language language, CancellationToken ct);
    Task<ContentTranslation?> GetContentTranslationAsync(long contentId, string targetLanguage, CancellationToken ct);
    Task<IReadOnlyList<ContentTranslation>> GetContentTranslationsAsync(long contentId, CancellationToken ct);
    Task<long> UpsertContentTranslationAsync(ContentTranslation translation, CancellationToken ct);
    Task<bool> SetContentTranslationStatusAsync(long id, TranslationReviewStatus status, long? reviewerId, string? notes, CancellationToken ct);
    Task<PlaceTranslation?> GetPlaceTranslationAsync(long placeId, string targetLanguage, CancellationToken ct);
    Task<IReadOnlyList<PlaceTranslation>> GetPlaceTranslationsAsync(long placeId, CancellationToken ct);
    Task<long> UpsertPlaceTranslationAsync(PlaceTranslation translation, CancellationToken ct);
    Task<bool> SetPlaceTranslationStatusAsync(long id, TranslationReviewStatus status, long? reviewerId, string? notes, CancellationToken ct);
    Task<IReadOnlyList<CulturalGlossaryItem>> GetCulturalGlossaryAsync(CancellationToken ct);
    Task<IReadOnlyList<ContentTranslation>> PendingContentTranslationsAsync(int take, CancellationToken ct);
    Task<IReadOnlyList<PlaceTranslation>> PendingPlaceTranslationsAsync(int take, CancellationToken ct);
    Task<TranslationStats> GetStatsAsync(CancellationToken ct);
    Task<IReadOnlyList<ContentItem>> UntranslatedContentAsync(string targetLanguage, int take, CancellationToken ct);
    Task<IReadOnlyList<Place>> UntranslatedPlacesAsync(string targetLanguage, int take, CancellationToken ct);
}

public interface ITouristRepository
{
    Task<IReadOnlyList<TouristItinerary>> GetItinerariesAsync(CancellationToken ct);
    Task<TouristItinerary?> GetItineraryBySlugAsync(string slug, CancellationToken ct);
    Task<IReadOnlyList<TouristItineraryStop>> GetItineraryStopsAsync(long itineraryId, CancellationToken ct);
    Task<IReadOnlyList<Place>> GetFeaturedPlacesAsync(int take, CancellationToken ct);
}

