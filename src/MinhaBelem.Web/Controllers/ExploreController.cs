using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Web.Models;
using MinhaBelem.Web.Resources;

namespace MinhaBelem.Web.Controllers;

[Route("explore")]
[Route("{culture:regex(^(pt-BR|en|fr|es|ru|zh-Hans|ja)$)}/explore")]
public sealed class ExploreController : Controller
{
    private readonly ITouristRepository tourist;
    private readonly IPlaceRepository places;
    private readonly ITranslationRepository translation;
    private readonly IStringLocalizer<SharedResource> loc;

    public ExploreController(
        ITouristRepository tourist,
        IPlaceRepository places,
        ITranslationRepository translation,
        IStringLocalizer<SharedResource> loc)
    {
        this.tourist = tourist;
        this.places = places;
        this.translation = translation;
        this.loc = loc;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? categoria = null,
        string? busca = null,
        int? dias = null,
        CancellationToken ct = default)
    {
        if (categoria?.Length > 80 || busca?.Length > 120) return BadRequest();

        var currentCulture = CultureInfo.CurrentUICulture.Name;
        var itineraries = await tourist.GetItinerariesAsync(ct);
        if (dias.HasValue && dias.Value > 0)
        {
            itineraries = itineraries.Where(i => i.Days == dias.Value).ToList();
        }

        var featuredPlaces = await tourist.GetFeaturedPlacesAsync(12, ct);
        var glossary = await translation.GetCulturalGlossaryAsync(ct);

        IReadOnlyList<Place> categorizedPlaces = Array.Empty<Place>();
        if (!string.IsNullOrWhiteSpace(categoria) || !string.IsNullOrWhiteSpace(busca))
        {
            categorizedPlaces = await places.SearchAsync(new PlaceQuery
            {
                Category = categoria,
                Search = busca,
                Culture = currentCulture,
                PageSize = 24
            }, ct);
        }

        ViewData["Title"] = loc["ExploreTitle"];
        ViewData["Description"] = loc["ExploreSubtitle"];

        var vm = new ExploreViewModel
        {
            Itineraries = itineraries,
            FeaturedPlaces = featuredPlaces,
            CategorizedPlaces = categorizedPlaces,
            CulturalTerms = glossary,
            SelectedCategory = categoria,
            SearchQuery = busca,
            SelectedDays = dias,
            CurrentCulture = currentCulture
        };

        return View(vm);
    }

    [HttpGet("roteiro/{slug}")]
    [HttpGet("{slug}")]
    public async Task<IActionResult> Roteiro(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > 120) return BadRequest();

        var itinerary = await tourist.GetItineraryBySlugAsync(slug, ct);
        if (itinerary is null) return NotFound();

        var currentCulture = CultureInfo.CurrentUICulture.Name;
        ViewData["Title"] = itinerary.GetTitle(currentCulture) + " — MinhaBelém";
        ViewData["Description"] = itinerary.GetSummary(currentCulture);

        return View(new ItineraryDetailViewModel
        {
            Itinerary = itinerary,
            CurrentCulture = currentCulture
        });
    }

    [HttpGet("glossario")]
    public async Task<IActionResult> Glossario(CancellationToken ct)
    {
        var terms = await translation.GetCulturalGlossaryAsync(ct);
        ViewData["Title"] = loc["CulturalGlossary"] + " — MinhaBelém";
        return View(terms);
    }
}
