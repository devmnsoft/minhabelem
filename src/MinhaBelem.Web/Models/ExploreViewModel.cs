using MinhaBelem.Core.Entities;

namespace MinhaBelem.Web.Models;

public sealed class ExploreViewModel
{
    public IReadOnlyList<TouristItinerary> Itineraries { get; init; } = Array.Empty<TouristItinerary>();
    public IReadOnlyList<Place> FeaturedPlaces { get; init; } = Array.Empty<Place>();
    public IReadOnlyList<Place> CategorizedPlaces { get; init; } = Array.Empty<Place>();
    public IReadOnlyList<CulturalGlossaryItem> CulturalTerms { get; init; } = Array.Empty<CulturalGlossaryItem>();
    public string? SelectedCategory { get; init; }
    public string? SearchQuery { get; init; }
    public int? SelectedDays { get; init; }
    public string CurrentCulture { get; init; } = "pt-BR";
}

public sealed class ItineraryDetailViewModel
{
    public TouristItinerary Itinerary { get; init; } = null!;
    public string CurrentCulture { get; init; } = "pt-BR";
}
