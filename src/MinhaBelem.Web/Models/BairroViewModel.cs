using MinhaBelem.Core.Entities;

namespace MinhaBelem.Web.Models;

public sealed class BairroIndexViewModel
{
    public IReadOnlyList<NeighborhoodGuide> Neighborhoods { get; init; } = Array.Empty<NeighborhoodGuide>();
    public string? SelectedZone { get; init; }
}

public sealed class BairroDetailViewModel
{
    public NeighborhoodGuide Guide { get; init; } = null!;
    public IReadOnlyList<ContentItem> LocalNews { get; init; } = Array.Empty<ContentItem>();
    public IReadOnlyList<Place> LocalPlaces { get; init; } = Array.Empty<Place>();
    public IReadOnlyList<NeighborhoodGuide> OtherNeighborhoods { get; init; } = Array.Empty<NeighborhoodGuide>();
}
