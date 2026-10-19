using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Infrastructure.Background;
using MinhaBelem.Infrastructure.Discovery;

namespace MinhaBelem.Web.Controllers;

[Route("api/v1"), IgnoreAntiforgeryToken, EnableRateLimiting("public")]
public sealed class PublicApiController(IContentRepository content, IPlaceRepository places, IPlacePhotoScout photos, IIngestionRepository ingestion, IOptions<PhotoScoutOptions> photoOptions, IOptions<CollectorOptions> collectorOptions) : Controller
{
    private static readonly Regex DeviceKey = new("^[a-f0-9]{64}$", RegexOptions.Compiled);

    [HttpGet("agenda")]
    public async Task<IActionResult> Agenda(int dias = 30, CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "public,max-age=120";
        var rows = await content.SearchAsync(new ContentQuery
        {
            OnlyUpcomingEvents = true,
            StartsBeforeUtc = DateTime.UtcNow.AddDays(Math.Clamp(dias, 1, 365)),
            PageSize = 60,
            Sort = ContentSort.Upcoming,
            Culture = CultureInfo.CurrentUICulture.Name
        }, ct);
        return Json(rows.Select(item => new
        {
            item.Id,
            title = item.DisplayTitle,
            summary = item.DisplaySummary,
            status = item.Status.ToString(),
            start = item.EventStartAt,
            place = item.Address ?? item.Neighborhood,
            item.Municipality,
            item.IsFree,
            price = item.TicketPriceText,
            source = item.SourceName,
            sourceUrl = item.SourceUrl,
            url = "/Home/Detalhe/" + item.Id
        }));
    }

    [HttpGet("lugares")]
    public async Task<IActionResult> Lugares(double? lat, double? lng, double? raioKm, CancellationToken ct = default)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180) return BadRequest();
        Response.Headers.CacheControl = "public,max-age=120";
        var radius = lat is null ? (double?)null : Math.Clamp(raioKm ?? 3, 0.3, 30);
        var rows = await places.SearchAsync(new PlaceQuery
        {
            PublishedOnly = true,
            Latitude = lat,
            Longitude = lng,
            RadiusKm = radius,
            Sort = lat is null ? PlaceSort.Name : PlaceSort.Distance,
            PageSize = 40,
            Culture = CultureInfo.CurrentUICulture.Name
        }, ct);
        return Json(rows.Select(place => new
        {
            place.Id,
            name = place.DisplayName,
            place.Neighborhood,
            place.Municipality,
            place.Latitude,
            place.Longitude,
            url = "/guia/" + place.Id
        }));
    }

    [HttpGet("fotos")]
    public async Task<IActionResult> Fotos(long? placeId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "public,max-age=300";
        var rows = await photos.ApprovedAsync(placeId, null, ct);
        return Json(rows.Select(photo => new
        {
            photo.Id, photo.PlaceId, photo.PlaceName, photo.Municipality, photo.ThumbUrl, photo.PageUrl, photo.Credit, photo.LicenseName, photo.LicenseUrl
        }));
    }

    [HttpGet("operacao")]
    public async Task<IActionResult> Operacao(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var desk = await photos.DeskAsync(ct);
        var sources = await ingestion.SourcesAsync(true, ct);
        var upcoming = await content.CountAsync(new ContentQuery { OnlyUpcomingEvents = true }, ct);
        return Json(new
        {
            product = "MinhaBelém",
            collectorEnabled = collectorOptions.Value.Enabled,
            photoScoutEnabled = photoOptions.Value.Enabled,
            operationalSources = sources.Count,
            upcomingEvents = upcoming,
            approvedPhotos = desk.Approved,
            pendingPhotos = desk.Pending,
            note = "A coleta contínua de sites só ocorre com fonte autorizada. Fotos públicas vêm de licença aberta e não informam horário nem preço."
        });
    }

    [HttpPost("salvos")]
    public async Task<IActionResult> Salvos([FromBody] SaveSync? body, CancellationToken ct)
    {
        if (body is null || body.Device is null || !DeviceKey.IsMatch(body.Device)) return BadRequest();
        var ids = (body.Events ?? []).Where(id => id > 0).Distinct().Take(200).ToArray();
        await photos.ReplaceSavesAsync(body.Device, ids, ct);
        var changes = await photos.ChangesAsync(ids, ct);
        return Json(new { saved = ids.Length, changes });
    }

    public sealed class SaveSync
    {
        public string? Device { get; set; }
        public long[]? Events { get; set; }
    }
}
