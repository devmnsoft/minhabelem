using MinhaBelem.Core.Entities;

namespace MinhaBelem.Web.Models;

public sealed class MobilidadeViewModel
{
    public IReadOnlyList<TransitLine> Lines { get; init; } = Array.Empty<TransitLine>();
    public IReadOnlyList<TransitAlert> Alerts { get; init; } = Array.Empty<TransitAlert>();
    public IReadOnlyList<RiverRoute> RiverRoutes { get; init; } = Array.Empty<RiverRoute>();
    public IReadOnlyList<TransitStop> Stops { get; init; } = Array.Empty<TransitStop>();
    public string? SelectedType { get; init; }
    public string? SearchQuery { get; init; }
}

public sealed class TransitLineDetailViewModel
{
    public TransitLine Line { get; init; } = null!;
    public IReadOnlyList<TransitStop> RelatedStops { get; init; } = Array.Empty<TransitStop>();
    public IReadOnlyList<TransitAlert> ActiveAlerts { get; init; } = Array.Empty<TransitAlert>();
}
