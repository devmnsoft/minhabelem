using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class TransitRepository(MySqlDataSource dataSource) : ITransitRepository
{
    // Colunas comuns de linha: inclui a proveniência editorial e a tabela de serviços.
    private const string LineColumns = """
        id, code, name, origin, destination, fare, transit_type AS TransitType, operator_name AS OperatorName,
        has_air_conditioning AS HasAirConditioning, has_wheelchair_lift AS HasWheelchairLift,
        itinerary_summary AS ItinerarySummary, service_days AS ServiceDays,
        first_departure AS FirstDeparture, last_departure AS LastDeparture, frequency_minutes AS FrequencyMinutes,
        integration_points AS IntegrationPoints, notes AS Notes,
        created_at AS CreatedAt, updated_at AS UpdatedAt,
        source_url AS SourceUrl, verified_at AS VerifiedAt, valid_until AS ValidUntil, fare_valid_until AS FareValidUntil
        """;

    public async Task<IReadOnlyList<TransitLine>> GetLinesAsync(string? transitType, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = $"SELECT {LineColumns} FROM mb_transit_lines";
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
        var sql = $"SELECT {LineColumns} FROM mb_transit_lines WHERE code = @code LIMIT 1";
        return await db.QuerySingleOrDefaultAsync<TransitLine>(new CommandDefinition(sql, new { code }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TransitLine>> SearchLinesAsync(string query, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = $"""
            SELECT {LineColumns} FROM mb_transit_lines
            WHERE code LIKE @pattern OR name LIKE @pattern OR origin LIKE @pattern OR destination LIKE @pattern OR itinerary_summary LIKE @pattern
               OR integration_points LIKE @pattern
            ORDER BY code ASC LIMIT 60
            """;
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

    public async Task<IReadOnlyList<TransitStop>> GetTerminalsAsync(CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT id, name, address, neighborhood, municipality, latitude, longitude, stop_type AS StopType,
                   is_accessible AS IsAccessible, lines_serviced AS LinesServiced, created_at AS CreatedAt
            FROM mb_transit_stops
            WHERE stop_type IN ('Terminal','Estação BRT')
            ORDER BY FIELD(stop_type,'Terminal','Estação BRT'), name ASC
            """;
        var rows = await db.QueryAsync<TransitStop>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TransitStop>> SearchStopsAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<TransitStop>();
        await using var db = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT id, name, address, neighborhood, municipality, latitude, longitude, stop_type AS StopType,
                   is_accessible AS IsAccessible, lines_serviced AS LinesServiced, created_at AS CreatedAt
            FROM mb_transit_stops
            WHERE name LIKE @pattern OR neighborhood LIKE @pattern OR lines_serviced LIKE @pattern
            ORDER BY name ASC LIMIT 40
            """;
        var rows = await db.QueryAsync<TransitStop>(new CommandDefinition(sql, new { pattern = $"%{query}%" }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TransitRoutePoint>> GetRoutePointsAsync(string lineCode, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT p.id AS Id, p.line_code AS LineCode, p.direction AS Direction, p.position AS Position,
                   p.label AS Label, p.kind AS Kind, p.stop_id AS StopId, s.name AS StopName
            FROM mb_transit_route_points p
            LEFT JOIN mb_transit_stops s ON s.id = p.stop_id
            WHERE p.line_code = @lineCode
            ORDER BY p.direction DESC, p.position ASC
            """;
        var rows = await db.QueryAsync<TransitRoutePoint>(new CommandDefinition(sql, new { lineCode }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TransitLine>> FindConnectingLinesAsync(string origin, string destination, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination)) return Array.Empty<TransitLine>();
        await using var db = await dataSource.OpenConnectionAsync(ct);
        // A linha conecta os dois pontos quando um dos lados casa com a origem e o outro
        // com o destino (nos dois sentidos de percurso).
        const string sql = """
            SELECT id, code, name, origin, destination, fare, transit_type AS TransitType, operator_name AS OperatorName,
                   has_air_conditioning AS HasAirConditioning, has_wheelchair_lift AS HasWheelchairLift,
                   itinerary_summary AS ItinerarySummary, service_days AS ServiceDays,
                   first_departure AS FirstDeparture, last_departure AS LastDeparture, frequency_minutes AS FrequencyMinutes,
                   integration_points AS IntegrationPoints, notes AS Notes,
                   created_at AS CreatedAt, updated_at AS UpdatedAt,
                   source_url AS SourceUrl, verified_at AS VerifiedAt, valid_until AS ValidUntil, fare_valid_until AS FareValidUntil
            FROM mb_transit_lines
            WHERE (
                    (origin LIKE @o AND destination LIKE @d)
                 OR (origin LIKE @d AND destination LIKE @o)
                 OR (origin LIKE @o AND integration_points LIKE @d)
                 OR (destination LIKE @d AND integration_points LIKE @o)
                 OR (integration_points LIKE @o AND integration_points LIKE @d)
            )
            ORDER BY code ASC
            """;
        var rows = await db.QueryAsync<TransitLine>(new CommandDefinition(sql, new { o = $"%{origin.Trim()}%", d = $"%{destination.Trim()}%" }, cancellationToken: ct));
        return rows.ToList();
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
        var sql = @"INSERT INTO mb_transit_lines (code, name, origin, destination, fare, transit_type, operator_name, has_air_conditioning, has_wheelchair_lift, itinerary_summary,service_days,first_departure,last_departure,frequency_minutes,integration_points,notes,source_url,verified_at,valid_until,fare_valid_until)
                    VALUES (@Code, @Name, @Origin, @Destination, @Fare, @TransitType, @OperatorName, @HasAirConditioning, @HasWheelchairLift, @ItinerarySummary,@ServiceDays,@FirstDeparture,@LastDeparture,@FrequencyMinutes,@IntegrationPoints,@Notes,@SourceUrl,@VerifiedAt,@ValidUntil,@FareValidUntil)
                    ON DUPLICATE KEY UPDATE
                        name = VALUES(name),
                        origin = VALUES(origin),
                        destination = VALUES(destination),
                        fare = VALUES(fare),
                        transit_type = VALUES(transit_type),
                        operator_name = VALUES(operator_name),
                        has_air_conditioning = VALUES(has_air_conditioning),
                        has_wheelchair_lift = VALUES(has_wheelchair_lift),
                        itinerary_summary = VALUES(itinerary_summary),
                        service_days = VALUES(service_days),
                        first_departure = VALUES(first_departure),
                        last_departure = VALUES(last_departure),
                        frequency_minutes = VALUES(frequency_minutes),
                        integration_points = VALUES(integration_points),
                        notes = VALUES(notes),
                        source_url=VALUES(source_url),
                        verified_at=VALUES(verified_at),
                        valid_until=VALUES(valid_until),
                        fare_valid_until=VALUES(fare_valid_until);
                    SELECT id  ,source_url AS SourceUrl,verified_at AS VerifiedAt,valid_until AS ValidUntil,fare_valid_until AS FareValidUntil FROM mb_transit_lines WHERE code = @Code;";
        return await db.ExecuteScalarAsync<long>(new CommandDefinition(sql, line, cancellationToken: ct));
    }
}
