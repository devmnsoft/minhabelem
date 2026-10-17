using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Web.Models;

namespace MinhaBelem.Web.Controllers;

[Route("meu-bairro")]
public sealed class BairroController(INeighborhoodRepository neighborhoods, IContentRepository content, IPlaceRepository places) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? zona, CancellationToken ct)
    {
        var all = await neighborhoods.GetNeighborhoodsAsync(ct);
        if (!string.IsNullOrWhiteSpace(zona))
        {
            all = all.Where(n => string.Equals(n.Zone, zona, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var vm = new BairroIndexViewModel
        {
            Neighborhoods = all,
            SelectedZone = zona
        };

        return View(vm);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Detalhe(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug)) return NotFound();
        var guide = await neighborhoods.GetBySlugAsync(slug.Trim().ToLowerInvariant(), ct);
        if (guide is null) return NotFound();

        // Buscar notícias e lugares do bairro
        var localNews = await content.SearchAsync(new ContentQuery { Neighborhood = guide.Name, Status = MinhaBelem.Core.Entities.ContentStatus.Published, PageSize = 6 }, ct);
        var localPlaces = await places.SearchAsync(new PlaceQuery { Neighborhood = guide.Name, PageSize = 6 }, ct);
        var allNeighborhoods = await neighborhoods.GetNeighborhoodsAsync(ct);

        var vm = new BairroDetailViewModel
        {
            Guide = guide,
            LocalNews = localNews,
            LocalPlaces = localPlaces,
            OtherNeighborhoods = allNeighborhoods.Where(n => n.Slug != guide.Slug).Take(4).ToList()
        };

        return View(vm);
    }
}
