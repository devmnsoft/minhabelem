using MinhaBelem.Core.Entities;

namespace MinhaBelem.Web.Models;

public sealed class MobilidadeViewModel
{
    public IReadOnlyList<TransitLine> Lines { get; init; } = Array.Empty<TransitLine>();
    public IReadOnlyList<TransitAlert> Alerts { get; init; } = Array.Empty<TransitAlert>();
    public IReadOnlyList<RiverRoute> RiverRoutes { get; init; } = Array.Empty<RiverRoute>();
    public IReadOnlyList<TransitStop> Stops { get; init; } = Array.Empty<TransitStop>();
    /// <summary>Terminais e estações de integração do sistema.</summary>
    public IReadOnlyList<TransitStop> Terminals { get; init; } = Array.Empty<TransitStop>();
    public string? SelectedType { get; init; }
    public string? SearchQuery { get; init; }
    public int LineCount => Lines.Count;
    public int TerminalCount => Terminals.Count;
}

public sealed class TransitLineDetailViewModel
{
    public TransitLine Line { get; init; } = null!;
    public IReadOnlyList<TransitStop> RelatedStops { get; init; } = Array.Empty<TransitStop>();
    public IReadOnlyList<TransitAlert> ActiveAlerts { get; init; } = Array.Empty<TransitAlert>();
    /// <summary>Itinerário oficial completo, com pontos na ordem de percurso (IDA e VOLTA).</summary>
    public IReadOnlyList<TransitRoutePoint> RoutePoints { get; init; } = Array.Empty<TransitRoutePoint>();
    /// <summary>Terminais e estações por onde a linha passa, na ordem do percurso.</summary>
    public IReadOnlyList<TransitStop> IntegrationPoints { get; init; } = Array.Empty<TransitStop>();

    public IReadOnlyList<TransitRoutePoint> Ida => RoutePoints.Where(p => p.Direction == "IDA").OrderBy(p => p.Position).ToArray();
    public IReadOnlyList<TransitRoutePoint> Volta => RoutePoints.Where(p => p.Direction == "VOLTA").OrderBy(p => p.Position).ToArray();
    public string? SourceUrl => Line.SourceUrl;
}

/// <summary>Resultado do planejador "de-para" de linhas.</summary>
public sealed class DeParaViewModel
{
    public string? Origin { get; init; }
    public string? Destination { get; init; }
    public IReadOnlyList<TransitLine> DirectLines { get; init; } = Array.Empty<TransitLine>();
    public IReadOnlyList<TransitStop> Terminals { get; init; } = Array.Empty<TransitStop>();
    public IReadOnlyList<TransitLine> AllLines { get; init; } = Array.Empty<TransitLine>();
    public bool HasQuery => !string.IsNullOrWhiteSpace(Origin) && !string.IsNullOrWhiteSpace(Destination);
    /// <summary>Terminal de integração em comum entre as linhas encontradas.</summary>
    public IReadOnlyList<TransitStop> SharedIntegrations { get; init; } = Array.Empty<TransitStop>();
}
