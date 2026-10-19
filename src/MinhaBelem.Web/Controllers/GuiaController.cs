using System.Globalization;
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Text;
using MinhaBelem.Infrastructure.Places;
using MinhaBelem.Web.Resources;

namespace MinhaBelem.Web.Controllers;

[Route("guia")]
[Route("{culture:regex(^(pt-BR|en|fr|es|ru|zh-Hans|ja)$)}/guia")]
public sealed class GuiaController(IPlaceRepository places, IPlacesClient google, IVisitRepository visits, IPlacePhotoScout photos, IVisitRequests requests) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? busca, string? bairro, string? municipio, string? categoria, Core.Taxonomy.Audience? publico, Core.Taxonomy.AccessibilityFeature? acessibilidade, double? lat, double? lng, double? raio, string? marco, CancellationToken ct)
    {
        if (busca?.Length > 120 || bairro?.Length > 120 || municipio?.Length > 120 || marco?.Length > 8) return BadRequest();
        if (lat is < -90 or > 90 || lng is < -180 or > 180) return BadRequest();
        if (categoria is not null && !PlacesCatalog.IsAllowedType(categoria) || publico is { } p && !Enum.IsDefined(p) || acessibilidade is { } a && !Enum.IsDefined(a)) return BadRequest();
        var currentCulture = CultureInfo.CurrentUICulture.Name;
        var near = lat is not null && lng is not null;
        var landmarks = marco is "1" or "true" or "on";
        ViewData["Near"] = near;
        ViewData["Landmarks"] = landmarks;
        var rows = await places.SearchAsync(new PlaceQuery
        {
            Search = busca, Neighborhood = bairro, Municipality = municipio, Category = categoria, Audience = publico, Accessibility = acessibilidade,
            Culture = currentCulture, PageSize = landmarks ? 60 : 24, Latitude = lat, Longitude = lng, RadiusKm = near ? Math.Clamp(raio ?? 3, 0.3, 30) : null,
            Sort = near ? PlaceSort.Distance : landmarks ? PlaceSort.Name : PlaceSort.Relevance,
            LandmarkOnly = landmarks
        }, ct);
        ViewData["Covers"] = await photos.CoversAsync(rows.Select(place => place.Id).ToArray(), ct);
        return View(rows);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalhe(long id, CancellationToken ct)
    {
        var stored = await places.GetAsync(id, ct);
        if (stored is null || !stored.IsPublished) return NotFound();
        Response.Headers.CacheControl = "no-store";
        ViewData["EditorialName"] = stored.Name;
        var own = LandmarkGuide.IsOwn(stored.GooglePlaceId);
        ViewData["OwnLandmark"] = own;
        Place? live = null;
        if (!own) live = await google.GetDetailsAsync(stored.GooglePlaceId, ct);
        if (live is not null)
        {
            live.Id = stored.Id; live.EditorialNote = stored.EditorialNote;
            live.CulturalNotes = stored.CulturalNotes;
            live.LocalizedName = stored.LocalizedName; live.LocalizedEditorialNote = stored.LocalizedEditorialNote;
            ViewData["Photo"] = await google.GetPhotoUriAsync(live.PhotoName, ct);
        }
        var schedule = await visits.GetAsync(id,ct);
        ViewData["VisitSchedule"] = schedule;
        ViewData["VisitAssessment"] = MinhaBelem.Core.Policies.VisitRecommendationService.Evaluate(schedule,DateTime.UtcNow,DateTime.UtcNow);
        ViewData["Live"] = live is not null;
        return View(live ?? stored);
    }

    [HttpPost("{id:long}/visita"), EnableRateLimiting("submission")]
    public async Task<IActionResult> Visita(long id, string? nome, string? email, string? telefone, int pessoas, DateOnly? data, string? recado, bool consentimento, CancellationToken ct)
    {
        var stored = await places.GetAsync(id, ct);
        if (stored is null || !stored.IsPublished) return NotFound();
        var culture = CultureInfo.CurrentUICulture.Name;
        var name = HtmlText.Clean(nome, 100);
        var mail = email?.Trim() ?? "";
        var phone = string.IsNullOrWhiteSpace(telefone) ? null : HtmlText.Clean(telefone, 30);
        var note = string.IsNullOrWhiteSpace(recado) ? null : HtmlText.ToPlainText(recado, 1000);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
        MailAddress.TryCreate(mail, out var parsed);
        var address = parsed?.Address;
        if (!consentimento || name.Length < 2 || address is null || mail.Length > 150 || !address.Contains('@') || pessoas is < 1 or > 30 || phone?.Length > 30 || data is null || data < today || data > today.AddDays(180))
        {
            TempData["VisitRequest"] = LocalizationCatalog.Get("VisitRequestInvalid", culture);
            return RedirectToAction(nameof(Detalhe), new { id });
        }
        await requests.SubmitAsync(new VisitRequest
        {
            PlaceId = id,
            ContactName = name,
            Email = address,
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
            PartySize = pessoas,
            PreferredOn = data.Value,
            Message = string.IsNullOrWhiteSpace(note) ? null : note
        }, ct);
        TempData["VisitRequest"] = LocalizationCatalog.Get("VisitRequestReceived", culture);
        return RedirectToAction(nameof(Detalhe), new { id });
    }
}
