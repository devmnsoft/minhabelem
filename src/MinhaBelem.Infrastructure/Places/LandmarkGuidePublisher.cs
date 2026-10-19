using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Services;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.Places;

public sealed class LandmarkGuidePublisher(MySqlDataSource data) : ILandmarkGuidePublisher
{
    public async Task<int> PublishAsync(CancellationToken ct)
    {
        await using var conn = await data.OpenConnectionAsync(ct);
        var targets = (await conn.QueryAsync<TargetRow>(new CommandDefinition("""
            SELECT target_key AS TargetKey, place_name AS PlaceName, municipality AS Municipality,
                   latitude AS Latitude, longitude AS Longitude, neighborhood AS Neighborhood,
                   address_line AS AddressLine, primary_type AS PrimaryType
            FROM mb_photo_targets WHERE enabled=1
            """, cancellationToken: ct))).AsList();
        var published = 0;
        foreach (var target in targets)
        {
            if (!BelemMetro.Contains(target.Latitude, target.Longitude)) continue;
            if (!PlacesCatalog.IsAllowedType(target.PrimaryType)) continue;
            try
            {
                if (await InsertAsync(conn, target, ct)) published++;
            }
            catch (MySqlException)
            {
                // Um slug ocupado por outro cadastro não impede os demais marcos.
            }
        }
        await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE mb_place_photo_proposals p
            JOIN mb_places pl ON pl.google_place_id COLLATE utf8mb4_unicode_ci =
                CONCAT(CONVERT('mb-landmark:' USING utf8mb4) COLLATE utf8mb4_unicode_ci, p.target_key COLLATE utf8mb4_unicode_ci)
            SET p.place_id = pl.id
            WHERE p.status='Approved' AND (p.place_id IS NULL OR p.place_id <> pl.id)
            """, cancellationToken: ct));
        var dirty = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*) FROM mb_places
            WHERE google_place_id LIKE 'mb-landmark:%'
              AND (opening_hours IS NOT NULL OR price_level IS NOT NULL OR price_text IS NOT NULL OR rating IS NOT NULL)
            """, cancellationToken: ct));
        if (dirty > 0) throw new InvalidOperationException("Um marco ficou com horário, preço ou nota. A ficha só guarda a coordenada.");
        return published;
    }

    private static async Task<bool> InsertAsync(MySqlConnection conn, TargetRow target, CancellationToken ct)
    {
        var id = LandmarkGuide.IdPrefix + target.TargetKey;
        var maps = LandmarkGuide.MapLink(target.Latitude, target.Longitude);
        var slug = await FreeSlugAsync(conn, target.TargetKey, id, ct);
        if (slug is null) return false;
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO mb_places
              (google_place_id, slug, name, formatted_address, neighborhood, municipality, state,
               latitude, longitude, google_maps_uri, primary_type, types, is_published,
               editorial_note, editorial_note_source, cached_at, purge_after)
            VALUES
              (@id, @slug, @PlaceName, @AddressLine, @Neighborhood, @Municipality, 'PA',
               @Latitude, @Longitude, @maps, @PrimaryType, JSON_ARRAY(@PrimaryType), 1,
               @note, 'Cadastro geográfico MinhaBelém', UTC_TIMESTAMP(6), DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 10 YEAR))
            ON DUPLICATE KEY UPDATE
              latitude=IF(google_place_id=@id, VALUES(latitude), latitude),
              longitude=IF(google_place_id=@id, VALUES(longitude), longitude),
              google_maps_uri=IF(google_place_id=@id, VALUES(google_maps_uri), google_maps_uri),
              formatted_address=IF(google_place_id=@id AND (formatted_address IS NULL OR formatted_address=''), VALUES(formatted_address), formatted_address),
              neighborhood=IF(google_place_id=@id AND (neighborhood IS NULL OR neighborhood=''), VALUES(neighborhood), neighborhood),
              primary_type=IF(google_place_id=@id AND (primary_type IS NULL OR primary_type=''), VALUES(primary_type), primary_type),
              purge_after=IF(google_place_id=@id, GREATEST(purge_after, VALUES(purge_after)), purge_after),
              is_published=IF(google_place_id=@id, 1, is_published)
            """, new
        {
            id, slug, target.PlaceName, target.AddressLine, target.Neighborhood, target.Municipality,
            target.Latitude, target.Longitude, maps, target.PrimaryType, note = LandmarkGuide.Note
        }, cancellationToken: ct));
        return true;
    }

    private static async Task<string?> FreeSlugAsync(MySqlConnection conn, string targetKey, string googleId, CancellationToken ct)
    {
        foreach (var candidate in new[] { targetKey, targetKey + "-marco" })
        {
            var owner = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT google_place_id FROM mb_places WHERE slug=@candidate LIMIT 1", new { candidate }, cancellationToken: ct));
            if (owner is null || owner == googleId) return candidate;
        }
        return null;
    }

    private sealed class TargetRow
    {
        public string TargetKey { get; set; } = "";
        public string PlaceName { get; set; } = "";
        public string Municipality { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Neighborhood { get; set; }
        public string? AddressLine { get; set; }
        public string? PrimaryType { get; set; }
    }
}
