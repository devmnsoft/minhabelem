using Dapper;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MySqlConnector;

namespace MinhaBelem.Web.Controllers;

/// <summary>
/// Superfície pública de vídeos e mídias: publicações do Radar, acervo de fotos
/// com licença verificada e arquivos de mídia autorizados (foto ou vídeo).
/// </summary>
public sealed class MediaSurfaceController(
    IRadarRepository? radar,
    IPlacePhotoScout photos,
    MySqlDataSource data) : Controller
{
    private const int PageSize = 24;

    [HttpGet("/videos")]
    [HttpGet("/{culture:regex(^(pt-BR|en|fr|es|ru|zh-Hans|ja)$)}/videos")]
    public async Task<IActionResult> Videos(int pagina = 1, CancellationToken ct = default)
    {
        if (pagina is < 1 or > 500) return BadRequest();

        var posts = radar is not null
            ? await radar.SocialPostsAsync(PageSize * 4, null, ct)
            : Array.Empty<Core.Entities.SocialPost>();

        var assets = await MediaAssetsAsync(ct);
        var archive = await photos.ApprovedAsync(null, null, ct);

        // Paginação apenas do acervo fotográfico: vídeos e publicações do Radar são lists em bloco único.
        var totalPages = Math.Max(1, (int)Math.Ceiling(archive.Count / (double)PageSize));
        var page = Math.Clamp(pagina, 1, totalPages);
        var archivePage = archive.Skip((page - 1) * PageSize).Take(PageSize).ToArray();

        ViewData["Title"] = "Vídeos";
        ViewData["Posts"] = posts;
        ViewData["Assets"] = assets;
        ViewData["Archive"] = archivePage;
        ViewData["Page"] = page;
        ViewData["PageCount"] = totalPages;
        return View("~/Views/Media/Videos.cshtml");
    }

    [HttpGet("/midias")]
    [HttpGet("/{culture:regex(^(pt-BR|en|fr|es|ru|zh-Hans|ja)$)}/midias")]
    public async Task<IActionResult> Midias(CancellationToken ct)
    {
        ViewData["Title"] = "Fotos e vídeos";
        ViewData["Assets"] = await MediaAssetsAsync(ct);
        ViewData["Archive"] = await photos.ApprovedAsync(null, null, ct);
        return View("~/Views/Media/Midias.cshtml");
    }

    /// <summary>
    /// Mídias autorizadas: só entram arquivos vinculados a conteúdo publicado ou a local publicado.
    /// </summary>
    private async Task<IReadOnlyList<PublicMediaAsset>> MediaAssetsAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = await data.OpenConnectionAsync(ct);
            var rows = await conn.QueryAsync<PublicMediaAsset>(new CommandDefinition("""
                SELECT m.id AS Id, m.description AS Description, m.credit AS Credit, m.license AS License,
                       m.youtube_id AS YoutubeId, m.storage_key AS StorageKey, m.mime_type AS MimeType
                FROM mb_media_assets m
                LEFT JOIN mb_content_items c ON c.id = m.content_id
                WHERE (c.id IS NOT NULL
                       AND c.status IN ('Published','Cancelled')
                       AND c.verification_status <> 'Outdated'
                       AND (c.expires_at IS NULL OR c.expires_at > UTC_TIMESTAMP()))
                   OR EXISTS(SELECT 1 FROM mb_place_media_assets pm JOIN mb_places p ON p.id = pm.place_id
                             WHERE pm.media_id = m.id AND p.is_published = 1 AND p.purge_after > UTC_TIMESTAMP())
                ORDER BY m.id DESC
                LIMIT 60
                """, cancellationToken: ct));
            return rows.AsList();
        }
        catch (MySqlException)
        {
            return Array.Empty<PublicMediaAsset>();
        }
    }
}

/// <summary>Mídia pública com licença verificada (foto local ou vídeo hospedado).</summary>
public sealed class PublicMediaAsset
{
    public long Id { get; set; }
    public string Description { get; set; } = "";
    public string Credit { get; set; } = "";
    public string License { get; set; } = "";
    public string? YoutubeId { get; set; }
    public string? StorageKey { get; set; }
    public string? MimeType { get; set; }

    public bool HasVideo => !string.IsNullOrWhiteSpace(YoutubeId) && YoutubeId.Length == 11;
    public bool HasPicture => !string.IsNullOrWhiteSpace(StorageKey) && !string.IsNullOrWhiteSpace(License);
    public bool IsVideoFile => !HasVideo && !string.IsNullOrWhiteSpace(StorageKey)
        && !string.IsNullOrWhiteSpace(MimeType) && MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    public string MediaUrl => HasVideo
        ? $"https://www.youtube-nocookie.com/embed/{YoutubeId}"
        : $"/midia/{Id}";
}
