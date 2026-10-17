using MinhaBelem.Core.Taxonomy;

namespace MinhaBelem.Core.Entities;

/// <summary>
/// Estabelecimento ou atração do guia. O <c>google_place_id</c> é o identificador estável que a
/// política do Google Places permite armazenar indefinidamente; todo o restante tem prazo de
/// expiração (<see cref="PurgeAfter"/>) e é removido pelo trabalho de expurgo.
/// </summary>
public class Place
{
    public string Slug { get; set; } = string.Empty;
    public long Id { get; set; }
    public string GooglePlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? FormattedAddress { get; set; }
    public string? Neighborhood { get; set; }
    public string? Municipality { get; set; }
    public string State { get; set; } = "PA";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? GoogleMapsUri { get; set; }
    public string? WebsiteUri { get; set; }
    public string? Phone { get; set; }
    public string? PrimaryType { get; set; }
    public IReadOnlyList<string> Types { get; set; } = Array.Empty<string>();
    public double? Rating { get; set; }
    public int? RatingCount { get; set; }
    public string? BusinessStatus { get; set; }
    public IReadOnlyList<OpeningHours> OpeningHours { get; set; } = Array.Empty<OpeningHours>();
    public int? PriceLevel { get; set; }
    public string? PriceText { get; set; }
    public bool? WheelchairAccessible { get; set; }
    public string? PhotoName { get; set; }
    public string? PhotoAttribution { get; set; }
    public string? PhotoGoogleMapsUri { get; set; }
    public IReadOnlyList<PhotoAuthor> PhotoAuthors { get; set; } = Array.Empty<PhotoAuthor>();
    public bool IsPublished { get; set; }
    public string? EditorialNote { get; set; }
    public string? EditorialNoteSource { get; set; }

    public string? LocalizedName { get; set; }
    public string? LocalizedEditorialNote { get; set; }
    public string? CulturalNotes { get; set; }

    public string DisplayName => !string.IsNullOrWhiteSpace(LocalizedName) ? LocalizedName : Name;
    public string DisplayEditorialNote => !string.IsNullOrWhiteSpace(LocalizedEditorialNote) ? LocalizedEditorialNote : (EditorialNote ?? string.Empty);

    public DateTime CachedAt { get; set; }
    public DateTime PurgeAfter { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public IReadOnlyList<Audience> Audiences { get; set; } = Array.Empty<Audience>();
    public IReadOnlyList<AccessibilityFeature> Accessibility { get; set; } = Array.Empty<AccessibilityFeature>();

    public bool IsStale(DateTime now) => PurgeAfter <= now;
}

public sealed record OpeningHours(int DayOfWeek, string Label, string OpenTime, string CloseTime, bool Closed);
public sealed record PhotoAuthor(string Name, string Uri)
{
    public string? PhotoUri { get; init; }
}

/// <summary>Filtro do guia combinando conteúdo editorial e lugares do Google Places.</summary>
public sealed class GuideFilter
{
    public ContentCategory? Category { get; set; }
    public string? Subject { get; set; }
    public string? Neighborhood { get; set; }
    public string? Municipality { get; set; }
    public Audience? Audience { get; set; }
    public AccessibilityFeature? Accessibility { get; set; }
    public bool? FreeOnly { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public string? TimeAfter { get; set; }
    public string? TimeBefore { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? RadiusKm { get; set; }
    public string? Query { get; set; }
    public PlaceSort Sort { get; set; } = PlaceSort.Relevance;
}

public enum PlaceSort { Relevance, Distance, Rating, Name }
