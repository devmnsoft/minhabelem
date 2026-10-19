using System.Security.Cryptography;
using System.Text;
using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Services;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.Discovery;

public sealed class PhotoScoutOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 12;
    public int TargetsPerRun { get; set; } = 3;
}

public sealed class PlacePhotoScout(MySqlDataSource data, IHttpClientFactory http, ILogger<PlacePhotoScout> logger) : IPlacePhotoScout
{
    private const string Agent = "MinhaBelemBot/1.0 (place photos; Belem, Para, Brazil)";

    public async Task<int> ScoutNextAsync(int maxTargets, CancellationToken ct)
    {
        await using var conn = await data.OpenConnectionAsync(ct);
        var locked = Convert.ToInt32(await conn.ExecuteScalarAsync(new CommandDefinition("SELECT GET_LOCK('minhabelem_photo_scout', 0)", cancellationToken: ct)));
        if (locked != 1) return 0;
        try
        {
            await conn.ExecuteAsync(new CommandDefinition("""
                UPDATE mb_photo_targets t
                SET last_scouted_at=NULL
                WHERE last_scouted_at > DATE_SUB(UTC_TIMESTAMP(6), INTERVAL 3 HOUR)
                  AND NOT EXISTS (SELECT 1 FROM mb_place_photo_proposals p WHERE p.target_key=t.target_key)
                """, cancellationToken: ct));
            var targets = (await conn.QueryAsync<PhotoTarget>(new CommandDefinition("""
                SELECT target_key AS TargetKey, place_name AS PlaceName, search_query AS SearchQuery, municipality AS Municipality,
                       latitude AS Latitude, longitude AS Longitude, radius_m AS RadiusM
                FROM mb_photo_targets
                WHERE enabled=1 AND (last_scouted_at IS NULL OR last_scouted_at < DATE_SUB(UTC_TIMESTAMP(6), INTERVAL 7 DAY))
                ORDER BY (last_scouted_at IS NULL) DESC, last_scouted_at
                LIMIT @take
                """, new { take = Math.Clamp(maxTargets, 1, 8) }, cancellationToken: ct))).AsList();
            var stored = 0;
            foreach (var target in targets)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    stored += await ScoutTargetAsync(conn, target, ct);
                    await conn.ExecuteAsync(new CommandDefinition("UPDATE mb_photo_targets SET last_scouted_at=UTC_TIMESTAMP(6) WHERE target_key=@TargetKey", target, cancellationToken: ct));
                }
                catch (OperationCanceledException) { throw; }
                catch (WikimediaThrottleException)
                {
                    logger.LogWarning("Wikimedia pediu pausa. Os locais restantes ficam para o próximo ciclo.");
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Foto de {Place} não consultada neste ciclo: {ErrorType}", target.PlaceName, ex.GetType().Name);
                }
                await Task.Delay(900, ct);
            }
            await LinkPlacesAsync(conn, ct);
            return stored;
        }
        finally
        {
            await conn.ExecuteAsync(new CommandDefinition("SELECT RELEASE_LOCK('minhabelem_photo_scout')", cancellationToken: CancellationToken.None));
        }
    }

    public async Task<int> CapturePageImageAsync(PageImageCapture capture, CancellationToken ct)
    {
        if (!PhotoLicense.IsPublishable(capture.License) || !SafeUrl.Validate(capture.ImageUrl).IsValid || !SafeUrl.Validate(capture.PageUrl).IsValid)
            return 0;
        if (capture.ImageUrl.Length > 2000 || capture.PageUrl.Length > 2000) return 0;
        var name = string.IsNullOrWhiteSpace(capture.Title) ? "Página consultada" : capture.Title.Trim();
        if (name.Length > 180) name = name[..180];
        var candidate = new PlacePhotoCandidate("pagina", name, "Belém", capture.ImageUrl.Trim(), capture.ImageUrl.Trim(), capture.PageUrl.Trim(),
            string.IsNullOrWhiteSpace(capture.Credit) ? "Crédito informado na página" : Trim(capture.Credit, 280), capture.License!.Trim(), null, null, null, null, null, null, true);
        await using var conn = await data.OpenConnectionAsync(ct);
        var saved = await SaveAsync(conn, candidate, autoApprove: false, ct);
        await LinkPlacesAsync(conn, ct);
        return saved;
    }

    public async Task<IReadOnlyList<PublicPlacePhoto>> ApprovedAsync(long? placeId, string? placeName, CancellationToken ct)
    {
        await using var conn = await data.OpenConnectionAsync(ct);
        var named = string.IsNullOrWhiteSpace(placeName) ? null : placeName.Trim();
        var filter = placeId is null && named is null ? "" : " AND (place_id = @placeId OR place_name = CONVERT(@placeName USING utf8mb4)) ";
        var rows = await conn.QueryAsync<PublicPlacePhoto>(new CommandDefinition($"""
            SELECT CAST(id AS SIGNED) AS Id, CAST(place_id AS SIGNED) AS PlaceId, place_name AS PlaceName, municipality AS Municipality,
                   COALESCE(thumb_url, image_url) AS ThumbUrl, page_url AS PageUrl, credit AS Credit,
                   license_name AS LicenseName, license_url AS LicenseUrl, status AS Status, publishable AS Publishable
            FROM mb_place_photo_proposals
            WHERE status='Approved' AND publishable=1
            {filter}
            ORDER BY place_name, id DESC
            LIMIT 240
            """, new { placeId, placeName = named }, cancellationToken: ct));
        return rows.Where(Displayable).ToArray();
    }

    public async Task<IReadOnlyDictionary<long, PublicPlacePhoto>> CoversAsync(IReadOnlyCollection<long> placeIds, CancellationToken ct)
    {
        if (placeIds.Count == 0) return new Dictionary<long, PublicPlacePhoto>();
        await using var conn = await data.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<PublicPlacePhoto>(new CommandDefinition("""
            SELECT CAST(p.id AS SIGNED) AS Id, CAST(p.place_id AS SIGNED) AS PlaceId, p.place_name AS PlaceName, p.municipality AS Municipality,
                   COALESCE(p.thumb_url, p.image_url) AS ThumbUrl, p.page_url AS PageUrl, p.credit AS Credit,
                   p.license_name AS LicenseName, p.license_url AS LicenseUrl, p.status AS Status, p.publishable AS Publishable
            FROM mb_place_photo_proposals p
            JOIN (
                SELECT place_id, MAX(id) AS id
                FROM mb_place_photo_proposals
                WHERE status='Approved' AND publishable=1 AND place_id IN @placeIds
                GROUP BY place_id
            ) pick ON pick.id = p.id
            """, new { placeIds }, cancellationToken: ct));
        return rows.Where(Displayable).Where(photo => photo.PlaceId is > 0)
            .GroupBy(photo => photo.PlaceId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
    }

    public async Task<(int Approved, int Pending, IReadOnlyList<PublicPlacePhoto> ApprovedRows, IReadOnlyList<PublicPlacePhoto> PendingRows)> DeskAsync(CancellationToken ct)
    {
        await using var conn = await data.OpenConnectionAsync(ct);
        var approved = await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_place_photo_proposals WHERE status='Approved' AND publishable=1", cancellationToken: ct));
        var pending = await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_place_photo_proposals WHERE status='Pending'", cancellationToken: ct));
        var rows = (await conn.QueryAsync<PublicPlacePhoto>(new CommandDefinition("""
            SELECT CAST(id AS SIGNED) AS Id, CAST(place_id AS SIGNED) AS PlaceId, place_name AS PlaceName, municipality AS Municipality,
                   COALESCE(thumb_url, image_url) AS ThumbUrl, page_url AS PageUrl, credit AS Credit,
                   license_name AS LicenseName, license_url AS LicenseUrl, status AS Status, publishable AS Publishable
            FROM mb_place_photo_proposals
            WHERE status IN ('Approved','Pending')
            ORDER BY status, discovered_at DESC
            LIMIT 80
            """, cancellationToken: ct))).Where(row => SafeUrl.Validate(row.ThumbUrl).IsValid && SafeUrl.Validate(row.PageUrl).IsValid).ToArray();
        return (approved, pending, rows.Where(r => r.Status == "Approved" && r.Publishable).ToArray(), rows.Where(r => r.Status == "Pending").ToArray());
    }

    public async Task<bool> ReviewAsync(long id, bool approve, long actorId, CancellationToken ct)
    {
        await using var conn = await data.OpenConnectionAsync(ct);
        if (!approve)
        {
            return await conn.ExecuteAsync(new CommandDefinition("UPDATE mb_place_photo_proposals SET status='Rejected', reviewed_at=UTC_TIMESTAMP(6), reviewed_by=@actorId WHERE id=@id AND status<>'Rejected'", new { id, actorId }, cancellationToken: ct)) == 1;
        }
        var license = await conn.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT license_name FROM mb_place_photo_proposals WHERE id=@id AND publishable=1", new { id }, cancellationToken: ct));
        if (!PhotoLicense.IsPublishable(license)) return false;
        return await conn.ExecuteAsync(new CommandDefinition("UPDATE mb_place_photo_proposals SET status='Approved', reviewed_at=UTC_TIMESTAMP(6), reviewed_by=@actorId WHERE id=@id AND publishable=1", new { id, actorId }, cancellationToken: ct)) == 1;
    }

    public async Task ReplaceSavesAsync(string deviceKey, IReadOnlyList<long> eventIds, CancellationToken ct)
    {
        if (deviceKey.Length != 64 || eventIds.Count > 200) return;
        var ids = eventIds.Where(id => id > 0).Distinct().Take(200).ToArray();
        await using var conn = await data.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM mb_resident_saves WHERE device_key=@deviceKey AND kind='event'", new { deviceKey }, tx, cancellationToken: ct));
        foreach (var id in ids)
            await conn.ExecuteAsync(new CommandDefinition("INSERT IGNORE INTO mb_resident_saves (device_key, kind, entity_id) VALUES (@deviceKey,'event',@id)", new { deviceKey, id }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<ResidentEventChange>> ChangesAsync(IReadOnlyList<long> eventIds, CancellationToken ct)
    {
        var ids = eventIds.Where(id => id > 0).Distinct().Take(80).ToArray();
        if (ids.Length == 0) return [];
        await using var conn = await data.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<ResidentEventChange>(new CommandDefinition("""
            SELECT CAST(id AS SIGNED) AS Id, title AS Title, status AS Status
            FROM mb_content_items
            WHERE id IN @ids AND status <> 'Published'
            """, new { ids }, cancellationToken: ct));
        return rows.ToArray();
    }

    private async Task<int> ScoutTargetAsync(MySqlConnection conn, PhotoTarget target, CancellationToken ct)
    {
        var client = http.CreateClient("feed");
        var radius = Math.Clamp(target.RadiusM, 100, 1000);
        var geo = "https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=geosearch"
            + "&ggscoord=" + target.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + target.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "&ggsradius=" + radius + "&ggsnamespace=6&ggslimit=12&prop=imageinfo&iiprop=url|mime|size|extmetadata&iiurlwidth=960";
        var found = await ReadCommonsAsync(client, geo, target, trustGeosearch: true, ct);
        if (found.Count < 3)
        {
            var search = "https://commons.wikimedia.org/w/api.php?action=query&format=json&generator=search&gsrnamespace=6&gsrlimit=8&prop=imageinfo&iiprop=url|mime|size|extmetadata&iiurlwidth=960&gsrsearch="
                + Uri.EscapeDataString(target.SearchQuery + " Belém Pará");
            found.AddRange(await ReadCommonsAsync(client, search, target, trustGeosearch: false, ct));
        }
        var saved = 0;
        foreach (var photo in found.DistinctBy(p => p.ImageUrl).Take(8))
            saved += await SaveAsync(conn, photo, autoApprove: true, ct);
        return saved;
    }

    private static async Task<List<PlacePhotoCandidate>> ReadCommonsAsync(HttpClient client, string url, PhotoTarget target, bool trustGeosearch, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(Agent);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if ((int)response.StatusCode == 429) throw new WikimediaThrottleException();
        if (!response.IsSuccessStatusCode) return [];
        var json = await response.Content.ReadAsStringAsync(ct);
        if (json.Length > 2_000_000) return [];
        return CommonsPhotoParser.Parse(json, target.TargetKey, target.PlaceName, target.Municipality, trustGeosearch).ToList();
    }

    private static async Task<int> SaveAsync(MySqlConnection conn, PlacePhotoCandidate photo, bool autoApprove, CancellationToken ct)
    {
        if (!photo.Publishable || photo.ImageUrl.Length > 2000 || photo.PageUrl.Length > 2000) return 0;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(photo.ImageUrl.Trim()))).ToLowerInvariant();
        var status = autoApprove ? "Approved" : "Pending";
        var affected = await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO mb_place_photo_proposals
            (target_key, place_name, municipality, image_url, thumb_url, page_url, credit, license_name, license_url, mime_type, width, height, latitude, longitude, image_hash, publishable, status)
            VALUES
            (@TargetKey, @PlaceName, @Municipality, @ImageUrl, @ThumbUrl, @PageUrl, @Credit, @LicenseName, @LicenseUrl, @Mime, @Width, @Height, @Latitude, @Longitude, @hash, 1, @status)
            ON DUPLICATE KEY UPDATE
              thumb_url=IF(status='Rejected', thumb_url, VALUES(thumb_url)),
              credit=IF(status='Rejected', credit, VALUES(credit))
            """, new
        {
            photo.TargetKey, photo.PlaceName, photo.Municipality, photo.ImageUrl, photo.ThumbUrl, photo.PageUrl,
            Credit = Trim(photo.Credit, 300), LicenseName = Trim(photo.LicenseName, 120), photo.LicenseUrl, photo.Mime,
            photo.Width, photo.Height, photo.Latitude, photo.Longitude, hash, status
        }, cancellationToken: ct));
        return affected > 0 ? 1 : 0;
    }

    private static async Task LinkPlacesAsync(MySqlConnection conn, CancellationToken ct) =>
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE mb_place_photo_proposals p
            JOIN mb_places pl ON pl.is_published=1 AND (
                pl.slug COLLATE utf8mb4_unicode_ci = p.target_key COLLATE utf8mb4_unicode_ci
                OR pl.name COLLATE utf8mb4_unicode_ci = p.place_name COLLATE utf8mb4_unicode_ci)
            SET p.place_id=pl.id
            WHERE p.place_id IS NULL
            """, cancellationToken: ct));

    private static bool Displayable(PublicPlacePhoto photo) =>
        photo.Publishable && photo.Status == "Approved" && SafeUrl.Validate(photo.ThumbUrl).IsValid && SafeUrl.Validate(photo.PageUrl).IsValid;

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];

    private sealed class WikimediaThrottleException : Exception;

    private sealed class PhotoTarget
    {
        public string TargetKey { get; set; } = "";
        public string PlaceName { get; set; } = "";
        public string SearchQuery { get; set; } = "";
        public string Municipality { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int RadiusM { get; set; }
    }
}
