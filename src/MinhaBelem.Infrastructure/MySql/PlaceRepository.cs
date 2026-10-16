using System.Text.Json;
using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Taxonomy;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class PlaceRepository(MySqlDataSource dataSource) : IPlaceRepository
{
    private const string Select = """
        SELECT id AS Id, google_place_id AS GooglePlaceId, slug AS Slug, name AS Name,
               formatted_address AS FormattedAddress, neighborhood AS Neighborhood,
               municipality AS Municipality, state AS State, latitude AS Latitude, longitude AS Longitude,
               google_maps_uri AS GoogleMapsUri, website_uri AS WebsiteUri, phone AS Phone,
               primary_type AS PrimaryType, types AS TypesJson, rating AS Rating, rating_count AS RatingCount,
               business_status AS BusinessStatus, opening_hours AS OpeningHoursJson, price_level AS PriceLevel,
               price_text AS PriceText, wheelchair_accessible AS WheelchairAccessible,
               photo_name AS PhotoName, photo_attribution AS PhotoAttribution, is_published AS IsPublished,
               editorial_note AS EditorialNote, editorial_note_source AS EditorialNoteSource,
               cached_at AS CachedAt, purge_after AS PurgeAfter, created_at AS CreatedAt, updated_at AS UpdatedAt
        FROM mb_places
        """;

    private sealed class PlaceRow : Place
    {
        public string? TypesJson { get; set; }
        public string? OpeningHoursJson { get; set; }
    }

    private static Place Map(PlaceRow row) => new()
    {
        Id = row.Id, GooglePlaceId = row.GooglePlaceId, Slug = row.Slug, Name = row.Name,
        FormattedAddress = row.FormattedAddress, Neighborhood = row.Neighborhood,
        Municipality = row.Municipality, State = row.State, Latitude = row.Latitude, Longitude = row.Longitude,
        GoogleMapsUri = row.GoogleMapsUri, WebsiteUri = row.WebsiteUri, Phone = row.Phone,
        PrimaryType = row.PrimaryType,
        Types = DeserializeList(row.TypesJson),
        Rating = row.Rating, RatingCount = row.RatingCount, BusinessStatus = row.BusinessStatus,
        OpeningHours = DeserializeHours(row.OpeningHoursJson),
        PriceLevel = row.PriceLevel, PriceText = row.PriceText,
        WheelchairAccessible = row.WheelchairAccessible,
        PhotoName = row.PhotoName, PhotoAttribution = row.PhotoAttribution, IsPublished = row.IsPublished,
        EditorialNote = row.EditorialNote, EditorialNoteSource = row.EditorialNoteSource,
        CachedAt = row.CachedAt, PurgeAfter = row.PurgeAfter, CreatedAt = row.CreatedAt, UpdatedAt = row.UpdatedAt
    };

    private static IReadOnlyList<string> DeserializeList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }

    private static IReadOnlyList<OpeningHours> DeserializeHours(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<OpeningHours>();
        try { return JsonSerializer.Deserialize<List<OpeningHours>>(json) ?? new List<OpeningHours>(); }
        catch (JsonException) { return Array.Empty<OpeningHours>(); }
    }

    private async Task<List<Place>> QueryAsync(string sql, object parameters, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var rows = (await connection.QueryAsync<PlaceRow>(new CommandDefinition(sql, parameters, cancellationToken: ct))).AsList();
        var places = rows.Select(Map).ToList();
        if (places.Count > 0) await LoadChildrenAsync(connection, places, ct);
        return places;
    }

    private static async Task LoadChildrenAsync(MySqlConnector.MySqlConnection connection, List<Place> places, CancellationToken ct)
    {
        var ids = places.Select(p => p.Id).ToArray();
        var audiences = await connection.QueryAsync<(long PlaceId, string Audience)>(new CommandDefinition(
            "SELECT place_id AS PlaceId, audience AS Audience FROM mb_place_audiences WHERE place_id IN @ids", new { ids }, cancellationToken: ct));
        var features = await connection.QueryAsync<(long PlaceId, string Feature)>(new CommandDefinition(
            "SELECT place_id AS PlaceId, feature AS Feature FROM mb_place_accessibility WHERE place_id IN @ids", new { ids }, cancellationToken: ct));

        var byAudience = audiences.Where(a => Enum.TryParse<Audience>(a.Audience, true, out _))
            .GroupBy(a => a.PlaceId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Audience>)g.Select(x => Enum.Parse<Audience>(x.Audience, true)).ToArray());
        var byFeature = features.Where(f => Enum.TryParse<AccessibilityFeature>(f.Feature, true, out _))
            .GroupBy(f => f.PlaceId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AccessibilityFeature>)g.Select(x => Enum.Parse<AccessibilityFeature>(x.Feature, true)).ToArray());

        foreach (var place in places)
        {
            if (byAudience.TryGetValue(place.Id, out var a)) place.Audiences = a;
            if (byFeature.TryGetValue(place.Id, out var f)) place.Accessibility = f;
        }
    }

    public async Task<Place?> GetByGoogleIdAsync(string googlePlaceId, CancellationToken ct) =>
        (await QueryAsync($"{Select} WHERE google_place_id = @googlePlaceId LIMIT 1", new { googlePlaceId }, ct)).FirstOrDefault();

    public async Task<Place?> GetAsync(long id, CancellationToken ct) =>
        (await QueryAsync($"{Select} WHERE id = @id LIMIT 1", new { id }, ct)).FirstOrDefault();

    public async Task<Place?> GetBySlugAsync(string slug, CancellationToken ct) =>
        (await QueryAsync($"{Select} WHERE slug = @slug LIMIT 1", new { slug }, ct)).FirstOrDefault();

    public async Task<IReadOnlyList<Place>> SearchAsync(PlaceQuery query, CancellationToken ct)
    {
        var clauses = new List<string>();
        var parameters = new DynamicParameters();

        if (query.PublishedOnly) clauses.Add("is_published = 1");
        clauses.Add("purge_after > UTC_TIMESTAMP(6)");
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            clauses.Add("primary_type = @category");
            parameters.Add("category", query.Category.Trim());
        }
        if (!string.IsNullOrWhiteSpace(query.Neighborhood))
        {
            clauses.Add("neighborhood = @neighborhood");
            parameters.Add("neighborhood", query.Neighborhood.Trim());
        }
        if (!string.IsNullOrWhiteSpace(query.Municipality))
        {
            clauses.Add("municipality = @municipality");
            parameters.Add("municipality", query.Municipality.Trim());
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            if (term.Length > 120) term = term[..120];
            clauses.Add("(name LIKE @search OR formatted_address LIKE @search)");
            parameters.Add("search", $"%{term.Replace("%", "\\%", StringComparison.Ordinal)}%");
        }
        if (query.Audience is not null)
        {
            clauses.Add("EXISTS (SELECT 1 FROM mb_place_audiences pa WHERE pa.place_id = mb_places.id AND pa.audience = @audience)");
            parameters.Add("audience", query.Audience.Value.ToString());
        }
        if (query.Accessibility is not null)
        {
            clauses.Add("EXISTS (SELECT 1 FROM mb_place_accessibility pf WHERE pf.place_id = mb_places.id AND pf.feature = @feature)");
            parameters.Add("feature", query.Accessibility.Value.ToString());
        }
        if (query.FreeOnly == true)
            clauses.Add("(price_level IS NULL OR price_level = 0)");
        if (query.Latitude is not null && query.Longitude is not null && query.RadiusKm is > 0)
        {
            // fencing de distância: 1 grau de latitude ~ 111,32 km; longitude corrigida pelo cosseno.
            clauses.Add("ST_Distance_Sphere(POINT(longitude, latitude), POINT(@lng, @lat)) <= @radiusMeters");
            parameters.Add("lat", query.Latitude.Value);
            parameters.Add("lng", query.Longitude.Value);
            parameters.Add("radiusMeters", query.RadiusKm.Value * 1000);
        }

        var order = query.Sort switch
        {
            PlaceSort.Rating => "rating IS NULL, rating DESC",
            PlaceSort.Name => "name ASC",
            PlaceSort.Distance when query.Latitude is not null && query.Longitude is not null =>
                "ST_Distance_Sphere(POINT(longitude, latitude), POINT(@lng, @lat)) ASC",
            _ => "rating IS NULL, rating DESC, name ASC"
        };
        if (query.Sort == PlaceSort.Distance) { parameters.Add("lat", query.Latitude); parameters.Add("lng", query.Longitude); }

        parameters.Add("limit", Math.Clamp(query.PageSize, 1, 60));
        parameters.Add("offset", (Math.Max(1, query.Page) - 1) * Math.Clamp(query.PageSize, 1, 60));

        var sql = $"{Select} WHERE {string.Join(" AND ", clauses)} ORDER BY {order} LIMIT @limit OFFSET @offset";
        return await QueryAsync(sql, parameters, ct);
    }

    public async Task<long> UpsertAsync(Place place, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        place.CachedAt = place.CachedAt == default ? now : place.CachedAt;
        place.UpdatedAt = now;
        place.CreatedAt = place.CreatedAt == default ? now : place.CreatedAt;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        var id = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            """
            INSERT INTO mb_places
              (google_place_id, slug, name, formatted_address, neighborhood, municipality, state,
               latitude, longitude, google_maps_uri, website_uri, phone, primary_type, types,
               rating, rating_count, business_status, opening_hours, price_level, price_text,
               wheelchair_accessible, photo_name, photo_attribution, is_published,
               editorial_note, editorial_note_source, cached_at, purge_after)
            VALUES
              (@GooglePlaceId, @Slug, @Name, @FormattedAddress, @Neighborhood, @Municipality, @State,
               @Latitude, @Longitude, @GoogleMapsUri, @WebsiteUri, @Phone, @PrimaryType, @Types,
               @Rating, @RatingCount, @BusinessStatus, @OpeningHours, @PriceLevel, @PriceText,
               @WheelchairAccessible, @PhotoName, @PhotoAttribution, @IsPublished,
               @EditorialNote, @EditorialNoteSource, @CachedAt, @PurgeAfter)
            ON DUPLICATE KEY UPDATE
              slug = VALUES(slug), name = VALUES(name),
              formatted_address = VALUES(formatted_address), neighborhood = VALUES(neighborhood),
              municipality = VALUES(municipality), latitude = VALUES(latitude),
              longitude = VALUES(longitude), google_maps_uri = VALUES(google_maps_uri),
              website_uri = VALUES(website_uri), phone = VALUES(phone),
              primary_type = VALUES(primary_type), types = VALUES(types),
              rating = VALUES(rating), rating_count = VALUES(rating_count),
              business_status = VALUES(business_status), opening_hours = VALUES(opening_hours),
              price_level = VALUES(price_level), price_text = VALUES(price_text),
              wheelchair_accessible = VALUES(wheelchair_accessible),
              photo_name = VALUES(photo_name), photo_attribution = VALUES(photo_attribution),
              cached_at = VALUES(cached_at), purge_after = VALUES(purge_after),
              editorial_note = VALUES(editorial_note), editorial_note_source = VALUES(editorial_note_source)
            """, new
            {
                place.GooglePlaceId, place.Slug, place.Name, place.FormattedAddress,
                place.Neighborhood, place.Municipality, place.State,
                place.Latitude, place.Longitude, place.GoogleMapsUri, place.WebsiteUri, place.Phone,
                place.PrimaryType,
                Types = JsonSerializer.Serialize(place.Types),
                place.Rating, place.RatingCount, place.BusinessStatus,
                OpeningHours = JsonSerializer.Serialize(place.OpeningHours),
                place.PriceLevel, place.PriceText, place.WheelchairAccessible,
                place.PhotoName, place.PhotoAttribution, place.IsPublished,
                place.EditorialNote, place.EditorialNoteSource,
                place.CachedAt, place.PurgeAfter
            }, tx, cancellationToken: ct));

        long placeId;
        if (id is > 0) placeId = id.Value;
        else
            placeId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT id FROM mb_places WHERE google_place_id = @googlePlaceId", new { place.GooglePlaceId }, tx, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM mb_place_audiences WHERE place_id = @id; DELETE FROM mb_place_accessibility WHERE place_id = @id;",
            new { id = placeId }, tx, cancellationToken: ct));
        if (place.Audiences.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO mb_place_audiences (place_id, audience) VALUES (@id, @audience);",
                place.Audiences.Select(a => new { id = placeId, audience = a.ToString() }), tx, cancellationToken: ct));
        if (place.Accessibility.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO mb_place_accessibility (place_id, feature) VALUES (@id, @feature);",
                place.Accessibility.Select(f => new { id = placeId, feature = f.ToString() }), tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        place.Id = placeId;
        return placeId;
    }

    public async Task<int> PurgeExpiredAsync(DateTime now, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM mb_places WHERE purge_after <= @now", new { now }, cancellationToken: ct));
    }

    public async Task<bool> SetPublishedAsync(long id, bool published, long actorId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE mb_places SET is_published = @published, updated_at = UTC_TIMESTAMP(6) WHERE id = @id;
            """, new { id, published }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> UpdateEditorialAsync(long id, string? note, string? noteSource, string? neighborhoods, CancellationToken ct)
    {
        var lists = (neighborhoods ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length is > 1 and <= 120)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE mb_places SET editorial_note = @note, editorial_note_source = @noteSource,
                neighborhood = @neighborhood, updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
            """,
            new { id, note, noteSource, neighborhood = lists.Length == 0 ? null : lists[0] },
            cancellationToken: ct)) > 0;
    }
}
