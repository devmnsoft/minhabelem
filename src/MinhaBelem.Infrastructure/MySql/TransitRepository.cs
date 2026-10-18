using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class TransitRepository(MySqlDataSource dataSource) : ITransitRepository
{
    public async Task<IReadOnlyList<TransitLine>> GetLinesAsync(string? transitType, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = "SELECT id, code, name, origin, destination, fare, transit_type AS TransitType, operator_name AS OperatorName, has_air_conditioning AS HasAirConditioning, has_wheelchair_lift AS HasWheelchairLift, itinerary_summary AS ItinerarySummary, created_at AS CreatedAt, updated_at AS UpdatedAt  ,source_url AS SourceUrl,verified_at AS VerifiedAt,valid_until AS ValidUntil,fare_valid_until AS FareValidUntil FROM mb_transit_lines";
        if (!string.IsNullOrWhiteSpace(transitType))
        {
            sql += " WHERE transit_type = @transitType";
            var rows = await db.QueryAsync<TransitLine>(new CommandDefinition(sql + " ORDER BY code ASC", new { transitType }, cancellationToken: ct));
            return rows.ToList();
        }
        var all = await db.QueryAsync<TransitLine>(new CommandDefinition(sql + " ORDER BY code ASC", cancellationToken: ct));
        return all.ToList();
    }

    public async Task<TransitLine?> GetLineByCodeAsync(string code, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = "SELECT id, code, name, origin, destination, fare, transit_type AS TransitType, operator_name AS OperatorName, has_air_conditioning AS HasAirConditioning, has_wheelchair_lift AS HasWheelchairLift, itinerary_summary AS ItinerarySummary, created_at AS CreatedAt, updated_at AS UpdatedAt  ,source_url AS SourceUrl,verified_at AS VerifiedAt,valid_until AS ValidUntil,fare_valid_until AS FareValidUntil FROM mb_transit_lines WHERE code = @code LIMIT 1";
        return await db.QuerySingleOrDefaultAsync<TransitLine>(new CommandDefinition(sql, new { code }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TransitLine>> SearchLinesAsync(string query, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = @"SELECT id, code, name, origin, destination, fare, transit_type AS TransitType, operator_name AS OperatorName, has_air_conditioning AS HasAirConditioning, has_wheelchair_lift AS HasWheelchairLift, itinerary_summary AS ItinerarySummary, created_at AS CreatedAt, updated_at AS UpdatedAt
                     ,source_url AS SourceUrl,verified_at AS VerifiedAt,valid_until AS ValidUntil,fare_valid_until AS FareValidUntil FROM mb_transit_lines
                    WHERE code LIKE @pattern OR name LIKE @pattern OR origin LIKE @pattern OR destination LIKE @pattern OR itinerary_summary LIKE @pattern
                    ORDER BY code ASC LIMIT 50";
        var rows = await db.QueryAsync<TransitLine>(new CommandDefinition(sql, new { pattern = $"%{query}%" }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TransitStop>> GetStopsAsync(string? neighborhood, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = "SELECT id, name, address, neighborhood, municipality, latitude, longitude, stop_type AS StopType, is_accessible AS IsAccessible, lines_serviced AS LinesServiced, created_at AS CreatedAt FROM mb_transit_stops";
        if (!string.IsNullOrWhiteSpace(neighborhood))
        {
            sql += " WHERE neighborhood = @neighborhood";
            var rows = await db.QueryAsync<TransitStop>(new CommandDefinition(sql + " ORDER BY name ASC", new { neighborhood }, cancellationToken: ct));
            return rows.ToList();
        }
        var all = await db.QueryAsync<TransitStop>(new CommandDefinition(sql + " ORDER BY name ASC", cancellationToken: ct));
        return all.ToList();
    }

    public async Task<IReadOnlyList<TransitAlert>> GetActiveAlertsAsync(CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = @"SELECT id, title, description, neighborhood, severity, alert_type AS AlertType, start_at AS StartAt, end_at AS EndAt, is_active AS IsActive, created_by_user_id AS CreatedByUserId, created_at AS CreatedAt, updated_at AS UpdatedAt
                    FROM mb_transit_alerts
                    WHERE is_active = TRUE AND start_at <= UTC_TIMESTAMP() AND (end_at IS NULL OR end_at > UTC_TIMESTAMP()) AND (valid_until IS NULL OR valid_until > UTC_TIMESTAMP())
                    ORDER BY start_at DESC LIMIT 30";
        var rows = await db.QueryAsync<TransitAlert>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<RiverRoute>> GetRiverRoutesAsync(CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = @"SELECT id, name, departure_port AS DeparturePort, destination_port AS DestinationPort, destination_island AS DestinationIsland, estimated_duration_minutes AS EstimatedDurationMinutes, fare, vessel_type AS VesselType, operating_hours AS OperatingHours, operator_notes AS OperatorNotes, is_active AS IsActive, created_at AS CreatedAt
                     ,source_url AS SourceUrl,verified_at AS VerifiedAt,valid_until AS ValidUntil,fare_valid_until AS FareValidUntil FROM mb_river_routes
                    WHERE is_active = TRUE
                    ORDER BY name ASC";
        var rows = await db.QueryAsync<RiverRoute>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<long> UpsertAlertAsync(TransitAlert alert, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = @"INSERT INTO mb_transit_alerts (title, description, neighborhood, severity, alert_type, start_at, end_at, is_active, created_by_user_id)
                    VALUES (@Title, @Description, @Neighborhood, @Severity, @AlertType, @StartAt, @EndAt, @IsActive, @CreatedByUserId);
                    SELECT LAST_INSERT_ID();";
        return await db.ExecuteScalarAsync<long>(new CommandDefinition(sql, alert, cancellationToken: ct));
    }

    public async Task<long> UpsertLineAsync(TransitLine line, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = @"INSERT INTO mb_transit_lines (code, name, origin, destination, fare, transit_type, operator_name, has_air_conditioning, has_wheelchair_lift, itinerary_summary,source_url,verified_at,valid_until,fare_valid_until)
                    VALUES (@Code, @Name, @Origin, @Destination, @Fare, @TransitType, @OperatorName, @HasAirConditioning, @HasWheelchairLift, @ItinerarySummary)
                    ON DUPLICATE KEY UPDATE
                        name = VALUES(name),
                        origin = VALUES(origin),
                        destination = VALUES(destination),
                        fare = VALUES(fare),
                        transit_type = VALUES(transit_type),
                        operator_name = VALUES(operator_name),
                        has_air_conditioning = VALUES(has_air_conditioning),
                        has_wheelchair_lift = VALUES(has_wheelchair_lift),
                        itinerary_summary = VALUES(itinerary_summary),source_url=VALUES(source_url),verified_at=VALUES(verified_at),valid_until=VALUES(valid_until),fare_valid_until=VALUES(fare_valid_until);
                    SELECT id  ,source_url AS SourceUrl,verified_at AS VerifiedAt,valid_until AS ValidUntil,fare_valid_until AS FareValidUntil FROM mb_transit_lines WHERE code = @Code;";
        return await db.ExecuteScalarAsync<long>(new CommandDefinition(sql, line, cancellationToken: ct));
    }
}
