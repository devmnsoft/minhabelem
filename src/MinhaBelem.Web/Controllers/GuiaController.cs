using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Services;
using MinhaBelem.Infrastructure.Places;

namespace MinhaBelem.Web.Controllers;

[Route("guia")]
[Route("{culture:regex(^(pt-BR|en|fr|es|ru|zh-Hans|ja)$)}/guia")]
public sealed class GuiaController(IPlaceRepository places, IPlacesClient google) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? busca, string? bairro, string? municipio, string? categoria, Core.Taxonomy.Audience? publico, Core.Taxonomy.AccessibilityFeature? acessibilidade, CancellationToken ct)
    {
        if (busca?.Length > 120 || bairro?.Length > 120 || municipio?.Length > 120) return BadRequest();
        if (categoria is not null && !PlacesCatalog.IsAllowedType(categoria) || publico is { } p && !Enum.IsDefined(p) || acessibilidade is { } a && !Enum.IsDefined(a)) return BadRequest();
        var currentCulture = CultureInfo.CurrentUICulture.Name;
        return View(await places.SearchAsync(new PlaceQuery { Search = busca, Neighborhood = bairro, Municipality = municipio, Category = categoria, Audience = publico, Accessibility = acessibilidade, Culture = currentCulture, PageSize = 24 }, ct));
    }
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalhe(long id, CancellationToken ct)
    {
        var stored = await places.GetAsync(id, ct);
        if (stored is null || !stored.IsPublished) return NotFound();
        Response.Headers.CacheControl = "no-store";
        ViewData["EditorialName"] = stored.Name;
        var live = await google.GetDetailsAsync(stored.GooglePlaceId, ct);
        if (live is not null)
        {
            live.Id = stored.Id; live.EditorialNote = stored.EditorialNote;
            ViewData["Photo"] = await google.GetPhotoUriAsync(live.PhotoName, ct);
        }
        ViewData["Live"] = live is not null;
        return View(live ?? stored);
    }
}
