using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MySqlConnector;

namespace MinhaBelem.Web.Controllers;

[Route("guia-ia")]
public sealed class TouristAssistantController(ITouristAssistant assistant, IPlacePhotoScout photos, MySqlDataSource data) : Controller
{
    public const int PageSize = 6;

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? q, [FromQuery] int pagina = 1, [FromQuery] bool useAi = true, CancellationToken ct = default)
    {
        if (pagina is < 1 or > 40) return BadRequest();
        if (string.IsNullOrWhiteSpace(q) || q.Length is < 3 or > 500)
            return View(Blank(useAi));
        Response.Headers.CacheControl = "no-store";
        var answer = await assistant.SearchAsync(q, CultureInfo.CurrentUICulture.Name, useAi, ct);
        return View(await PageAsync(q, answer, pagina, useAi, ct));
    }

    [HttpPost(""), ValidateAntiForgeryToken, Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("assistant")]
    public async Task<IActionResult> Search(string question, bool useAi = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length is < 3 or > 500) return BadRequest();
        Response.Headers.CacheControl = "no-store";
        var answer = await assistant.SearchAsync(question, CultureInfo.CurrentUICulture.Name, useAi, ct);
        return View("Index", await PageAsync(question, answer, 1, useAi, ct));
    }

    private static TouristAssistantPage Blank(bool useAi) =>
        new("", null, 1, 1, useAi, new Dictionary<long, PublicPlacePhoto>(), Array.Empty<AiClip>());

    private async Task<TouristAssistantPage> PageAsync(string question, TouristAnswer answer, int page, bool useAi, CancellationToken ct)
    {
        var pages = Math.Max(1, (int)Math.Ceiling(Math.Max(answer.Places.Count, Math.Max(answer.Content.Count, answer.Lines.Count)) / (double)PageSize));
        page = Math.Clamp(page, 1, pages);
        var skip = (page - 1) * PageSize;
        var placeIds = answer.Places.Skip(skip).Take(PageSize).Select(place => place.Id).ToArray();
        var contentIds = answer.Content.Skip(skip).Take(PageSize).Select(item => item.Id).ToArray();
        var covers = await photos.CoversAsync(placeIds, ct);
        var clips = await ClipsAsync(placeIds, contentIds, ct);
        return new(question, answer, page, pages, useAi, covers, clips);
    }

    private async Task<IReadOnlyList<AiClip>> ClipsAsync(long[] placeIds, long[] contentIds, CancellationToken ct)
    {
        if (placeIds.Length == 0 && contentIds.Length == 0) return Array.Empty<AiClip>();
        try
        {
            await using var conn = await data.OpenConnectionAsync(ct);
            var rows = new List<AiClip>();
            if (placeIds.Length > 0)
            {
                rows.AddRange(await conn.QueryAsync<AiClip>(new CommandDefinition("""
                    SELECT CAST(m.id AS SIGNED) AS Id, CAST(pm.place_id AS SIGNED) AS PlaceId,
                           m.description AS Description, m.credit AS Credit, m.license AS License,
                           m.youtube_id AS YoutubeId, m.storage_key AS StorageKey
                    FROM mb_media_assets m
                    JOIN mb_place_media_assets pm ON pm.media_id = m.id
                    WHERE pm.place_id IN @placeIds
                      AND (m.youtube_id IS NOT NULL OR m.storage_key IS NOT NULL)
                    ORDER BY m.id DESC
                    LIMIT 12
                    """, new { placeIds }, cancellationToken: ct)));
            }
            if (contentIds.Length > 0)
            {
                rows.AddRange(await conn.QueryAsync<AiClip>(new CommandDefinition("""
                    SELECT CAST(id AS SIGNED) AS Id, CAST(content_id AS SIGNED) AS ContentId,
                           description AS Description, credit AS Credit, license AS License,
                           youtube_id AS YoutubeId, storage_key AS StorageKey
                    FROM mb_media_assets
                    WHERE content_id IN @contentIds
                      AND (youtube_id IS NOT NULL OR storage_key IS NOT NULL)
                    ORDER BY id DESC
                    LIMIT 12
                    """, new { contentIds }, cancellationToken: ct)));
            }
            return rows.Where(clip => clip.HasPicture || clip.HasVideo).Take(8).ToArray();
        }
        catch (MySqlException)
        {
            return Array.Empty<AiClip>();
        }
    }
}

public sealed record TouristAssistantPage(
    string Question,
    TouristAnswer? Answer,
    int Page,
    int PageCount,
    bool UseAi,
    IReadOnlyDictionary<long, PublicPlacePhoto> Covers,
    IReadOnlyList<AiClip> Clips);

public sealed class AiClip
{
    public long Id { get; set; }
    public long? PlaceId { get; set; }
    public long? ContentId { get; set; }
    public string Description { get; set; } = "";
    public string Credit { get; set; } = "";
    public string License { get; set; } = "";
    public string? YoutubeId { get; set; }
    public string? StorageKey { get; set; }

    public bool HasVideo => YoutubeId is not null && Youtube.IsMatch(YoutubeId);
    public bool HasPicture => !string.IsNullOrWhiteSpace(StorageKey) && !string.IsNullOrWhiteSpace(License);

    private static readonly Regex Youtube = new("^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
