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

        var vm = new MobilidadeViewModel
        {
            Lines = lines,
            Alerts = alerts,
            RiverRoutes = riverRoutes,
            Stops = stops,
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

        var vm = new TransitLineDetailViewModel
        {
            Line = line,
            RelatedStops = stops.Where(s => (s.LinesServiced ?? "").Split(new[]{',',';','|'}, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(line.Code, StringComparer.OrdinalIgnoreCase)).ToArray(),
            ActiveAlerts = alerts
        };

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
