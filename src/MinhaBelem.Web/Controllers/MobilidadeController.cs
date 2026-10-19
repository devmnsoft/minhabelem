using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Web.Models;

namespace MinhaBelem.Web.Controllers;

[Route("mobilidade")]
public sealed class MobilidadeController(ITransitRepository transit) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? tipo, [FromQuery] string? busca, CancellationToken ct)
    {
        IReadOnlyList<MinhaBelem.Core.Entities.TransitLine> lines;
        if (!string.IsNullOrWhiteSpace(busca))
        {
            lines = await transit.SearchLinesAsync(busca.Trim(), ct);
        }
        else
        {
            lines = await transit.GetLinesAsync(tipo, ct);
        }

        var alerts = await transit.GetActiveAlertsAsync(ct);
        var riverRoutes = await transit.GetRiverRoutesAsync(ct);
        var stops = await transit.GetStopsAsync(null, ct);
        var terminals = await transit.GetTerminalsAsync(ct);

        var vm = new MobilidadeViewModel
        {
            Lines = lines,
            Alerts = alerts,
            RiverRoutes = riverRoutes,
            Stops = stops,
            Terminals = terminals,
            SelectedType = tipo,
            SearchQuery = busca
        };

        return View(vm);
    }

    [HttpGet("linha/{code}")]
    public async Task<IActionResult> Linha(string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return NotFound();
        var line = await transit.GetLineByCodeAsync(code.Trim(), ct);
        if (line is null) return NotFound();

        var alerts = await transit.GetActiveAlertsAsync(ct);
        var stops = await transit.GetStopsAsync(null, ct);
        var routePoints = await transit.GetRoutePointsAsync(line.Code, ct);

        // Terminais e estações efetivamente atravessados pela linha, na ordem do percurso.
        var integrationIds = routePoints
            .Where(p => p.StopId.HasValue)
            .GroupBy(p => p.StopId!.Value)
            .OrderBy(g => g.Min(p => p.Position))
            .Select(g => g.Key)
            .ToHashSet();

        var vm = new TransitLineDetailViewModel
        {
            Line = line,
            RelatedStops = stops.Where(s => (s.LinesServiced ?? "").Split(new[]{',',';','|'}, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(line.Code, StringComparer.OrdinalIgnoreCase)).ToArray(),
            ActiveAlerts = alerts,
            RoutePoints = routePoints,
            IntegrationPoints = stops.Where(s => integrationIds.Contains(s.Id)).ToArray()
        };

        return View(vm);
    }

    /// <summary>Terminais, estações e pontos de embarque do sistema.</summary>
    [HttpGet("paradas")]
    public async Task<IActionResult> Paradas([FromQuery] string? bairro, [FromQuery] string? busca, CancellationToken ct)
    {
        var stops = await transit.GetStopsAsync(string.IsNullOrWhiteSpace(bairro) ? null : bairro.Trim(), ct);
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var term = busca.Trim();
            stops = stops.Where(s =>
                s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (s.Neighborhood ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (s.LinesServiced ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        ViewData["Bairro"] = bairro;
        ViewData["Busca"] = busca;
        ViewData["Title"] = "Paradas e terminais";
        return View(stops);
    }

    /// <summary>Planejador "de-para": encontra as linhas que conectam dois pontos.</summary>
    [HttpGet("de-para")]
    public async Task<IActionResult> DePara([FromQuery] string? origem, [FromQuery] string? destino, CancellationToken ct)
    {
        var all = await transit.GetLinesAsync(null, ct);
        var terminals = await transit.GetTerminalsAsync(ct);

        var direct = Array.Empty<MinhaBelem.Core.Entities.TransitLine>();
        var shared = Array.Empty<MinhaBelem.Core.Entities.TransitStop>();

        if (!string.IsNullOrWhiteSpace(origem) && !string.IsNullOrWhiteSpace(destino))
        {
            direct = (await transit.FindConnectingLinesAsync(origem.Trim(), destino.Trim(), ct)).ToArray();
            if (direct.Length > 0)
            {
                // Terminais presentes em todas as linhas encontradas: pontos de integração sugeridos.
                var common = direct
                    .Select(l => (l.IntegrationPoints ?? "").Split(new[] { ',', ';', '|' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim()).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase))
                    .Aggregate((a, b) => a.Intersect(b, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                shared = terminals.Where(t => common.Any(c => c.Contains(t.Name, StringComparison.OrdinalIgnoreCase) || t.Name.Contains(c, StringComparison.OrdinalIgnoreCase))).ToArray();
            }
        }

        var vm = new DeParaViewModel
        {
            Origin = origem,
            Destination = destino,
            DirectLines = direct,
            Terminals = terminals,
            AllLines = all,
            SharedIntegrations = shared
        };
        ViewData["Title"] = "Planejador de rotas de transporte";
        return View(vm);
    }

    [HttpGet("fluvial")]
    public async Task<IActionResult> Fluvial(CancellationToken ct)
    {
        var routes = await transit.GetRiverRoutesAsync(ct);
        return View(routes);
    }

    [HttpGet("alertas")]
    public async Task<IActionResult> Alertas(CancellationToken ct)
    {
        var alerts = await transit.GetActiveAlertsAsync(ct);
        return View(alerts);
    }
}
