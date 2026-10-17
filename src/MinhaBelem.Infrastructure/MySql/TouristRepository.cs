using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class TouristRepository(MySqlDataSource dataSource) : ITouristRepository
{
    private async Task<MySqlConnector.MySqlConnection> OpenAsync(CancellationToken ct)
        => (MySqlConnector.MySqlConnection)await dataSource.OpenConnectionAsync(ct);

    public async Task<IReadOnlyList<TouristItinerary>> GetItinerariesAsync(CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            SELECT id AS Id, slug AS Slug, days AS Days, theme AS Theme,
                   title_pt AS TitlePt, title_en AS TitleEn, title_fr AS TitleFr,
                   title_es AS TitleEs, title_ru AS TitleRu, title_zh AS TitleZh, title_ja AS TitleJa,
                   summary_pt AS SummaryPt, summary_en AS SummaryEn, summary_fr AS SummaryFr,
                   summary_es AS SummaryEs, summary_ru AS SummaryRu, summary_zh AS SummaryZh, summary_ja AS SummaryJa,
                   description_pt AS DescriptionPt, description_en AS DescriptionEn, description_fr AS DescriptionFr,
                   description_es AS DescriptionEs, description_ru AS DescriptionRu, description_zh AS DescriptionZh, description_ja AS DescriptionJa,
                   display_order AS DisplayOrder, is_published AS IsPublished, created_at AS CreatedAt
            FROM mb_tourist_itineraries
            WHERE is_published = 1
            ORDER BY display_order ASC, days ASC
            """;
        var itineraries = (await db.QueryAsync<TouristItinerary>(new CommandDefinition(sql, cancellationToken: ct))).AsList();

        if (itineraries.Count > 0)
        {
            var stops = (await db.QueryAsync<TouristItineraryStop>(new CommandDefinition("""
                SELECT s.id AS Id, s.itinerary_id AS ItineraryId, s.day_number AS DayNumber,
                       s.stop_order AS StopOrder, s.place_id AS PlaceId,
                       s.title_pt AS TitlePt, s.title_en AS TitleEn, s.period_of_day AS PeriodOfDay,
                       s.tip_pt AS TipPt, s.tip_en AS TipEn, s.tip_fr AS TipFr, s.tip_es AS TipEs,
                       s.tip_ru AS TipRu, s.tip_zh AS TipZh, s.tip_ja AS TipJa,
                       s.estimated_duration AS EstimatedDuration
                FROM mb_tourist_itinerary_stops s
                ORDER BY s.day_number ASC, s.stop_order ASC
                """, cancellationToken: ct))).AsList();

            var stopsByItinerary = stops.GroupBy(s => s.ItineraryId).ToDictionary(g => g.Key, g => (IReadOnlyList<TouristItineraryStop>)g.ToList());
            foreach (var it in itineraries)
            {
                if (stopsByItinerary.TryGetValue(it.Id, out var itStops))
                    it.Stops = itStops;
            }
        }

        return itineraries;
    }

    public async Task<TouristItinerary?> GetItineraryBySlugAsync(string slug, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            SELECT id AS Id, slug AS Slug, days AS Days, theme AS Theme,
                   title_pt AS TitlePt, title_en AS TitleEn, title_fr AS TitleFr,
                   title_es AS TitleEs, title_ru AS TitleRu, title_zh AS TitleZh, title_ja AS TitleJa,
                   summary_pt AS SummaryPt, summary_en AS SummaryEn, summary_fr AS SummaryFr,
                   summary_es AS SummaryEs, summary_ru AS SummaryRu, summary_zh AS SummaryZh, summary_ja AS SummaryJa,
                   description_pt AS DescriptionPt, description_en AS DescriptionEn, description_fr AS DescriptionFr,
                   description_es AS DescriptionEs, description_ru AS DescriptionRu, description_zh AS DescriptionZh, description_ja AS DescriptionJa,
                   display_order AS DisplayOrder, is_published AS IsPublished, created_at AS CreatedAt
            FROM mb_tourist_itineraries
            WHERE slug = @slug AND is_published = 1
            LIMIT 1
            """;
        var itinerary = await db.QuerySingleOrDefaultAsync<TouristItinerary>(new CommandDefinition(sql, new { slug }, cancellationToken: ct));
        if (itinerary is not null)
        {
            itinerary.Stops = await GetItineraryStopsAsync(itinerary.Id, ct);
        }
        return itinerary;
    }

    public async Task<IReadOnlyList<TouristItineraryStop>> GetItineraryStopsAsync(long itineraryId, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            SELECT s.id AS Id, s.itinerary_id AS ItineraryId, s.day_number AS DayNumber,
                   s.stop_order AS StopOrder, s.place_id AS PlaceId,
                   s.title_pt AS TitlePt, s.title_en AS TitleEn, s.period_of_day AS PeriodOfDay,
                   s.tip_pt AS TipPt, s.tip_en AS TipEn, s.tip_fr AS TipFr, s.tip_es AS TipEs,
                   s.tip_ru AS TipRu, s.tip_zh AS TipZh, s.tip_ja AS TipJa,
                   s.estimated_duration AS EstimatedDuration,
                   p.name AS PlaceName, p.google_place_id AS PlaceGoogleId, p.photo_name AS PlacePhotoName,
                   p.formatted_address AS PlaceAddress, p.rating AS PlaceRating
            FROM mb_tourist_itinerary_stops s
            LEFT JOIN mb_places p ON p.id = s.place_id
            WHERE s.itinerary_id = @itineraryId
            ORDER BY s.day_number ASC, s.stop_order ASC
            """;
        var rows = await db.QueryAsync<dynamic>(new CommandDefinition(sql, new { itineraryId }, cancellationToken: ct));
        var stops = new List<TouristItineraryStop>();
        foreach (var row in rows)
        {
            var stop = new TouristItineraryStop
            {
                Id = row.Id,
                ItineraryId = row.ItineraryId,
                DayNumber = row.DayNumber,
                StopOrder = row.StopOrder,
                PlaceId = row.PlaceId,
                TitlePt = row.TitlePt,
                TitleEn = row.TitleEn,
                PeriodOfDay = row.PeriodOfDay,
                TipPt = row.TipPt,
                TipEn = row.TipEn,
                TipFr = row.TipFr,
                TipEs = row.TipEs,
                TipRu = row.TipRu,
                TipZh = row.TipZh,
                TipJa = row.TipJa,
                EstimatedDuration = row.EstimatedDuration
            };
            if (row.PlaceId != null)
            {
                stop.Place = new Place
                {
                    Id = row.PlaceId,
                    Name = row.PlaceName ?? string.Empty,
                    GooglePlaceId = row.PlaceGoogleId ?? string.Empty,
                    PhotoName = row.PlacePhotoName,
                    FormattedAddress = row.PlaceAddress,
                    Rating = (double?)row.PlaceRating
                };
            }
            stops.Add(stop);
        }
        return stops;
    }

    public async Task<IReadOnlyList<Place>> GetFeaturedPlacesAsync(int take, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            SELECT id AS Id, google_place_id AS GooglePlaceId, slug AS Slug, name AS Name,
                   formatted_address AS FormattedAddress, neighborhood AS Neighborhood,
                   municipality AS Municipality, state AS State, latitude AS Latitude, longitude AS Longitude,
                   google_maps_uri AS GoogleMapsUri, website_uri AS WebsiteUri, phone AS Phone,
                   primary_type AS PrimaryType, rating AS Rating, rating_count AS RatingCount,
                   photo_name AS PhotoName, photo_attribution AS PhotoAttribution, is_published AS IsPublished,
                   editorial_note AS EditorialNote, editorial_note_source AS EditorialNoteSource
            FROM mb_places
            WHERE is_published = 1 AND purge_after > UTC_TIMESTAMP(6)
            ORDER BY rating DESC, rating_count DESC, name ASC
            LIMIT @take
            """;
        return (await db.QueryAsync<Place>(new CommandDefinition(sql, new { take }, cancellationToken: ct))).AsList();
    }
}
