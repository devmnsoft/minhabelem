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

    public async Task<IReadOnlyList<RiverRoute>> GetRiverRoutesAsync(CancellationToken ct,bool includeInactive=false)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var sql = @"SELECT id, name, departure_port AS DeparturePort, destination_port AS DestinationPort, destination_island AS DestinationIsland, estimated_duration_minutes AS EstimatedDurationMinutes, fare, vessel_type AS VesselType, operating_hours AS OperatingHours, operator_notes AS OperatorNotes, is_active AS IsActive, created_at AS CreatedAt
                     ,source_url AS SourceUrl,verified_at AS VerifiedAt,valid_until AS ValidUntil,fare_valid_until AS FareValidUntil,operator_name AS OperatorName,is_accessible AS IsAccessible,revision AS Revision FROM mb_river_routes
                    WHERE is_active = TRUE OR @includeInactive=TRUE
                    ORDER BY name ASC";
        var rows = await db.QueryAsync<RiverRoute>(new CommandDefinition(sql,new{includeInactive}, cancellationToken: ct));
        return rows.ToList();
    }
    public async Task<bool> SaveRiverRouteAsync(RiverRoute route,int expectedRevision,long actorId,CancellationToken ct)
    {
        await using var db=await dataSource.OpenConnectionAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);
        var previous=route.Id==0?null:await db.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT JSON_OBJECT('id',id,'name',name,'source',source_url,'hours',operating_hours,'fare',fare,'revision',revision) FROM mb_river_routes WHERE id=@Id AND revision=@expectedRevision FOR UPDATE",new{route.Id,expectedRevision},tx,cancellationToken:ct));
        if(route.Id!=0&&previous is null)return false;
        if(route.Id==0)route.Id=await db.ExecuteScalarAsync<long>(new CommandDefinition("INSERT INTO mb_river_routes(name,departure_port,destination_port,destination_island,estimated_duration_minutes,fare,vessel_type,operating_hours,operator_notes,is_active,source_url,verified_at,valid_until,fare_valid_until,operator_name,is_accessible) VALUES(@Name,@DeparturePort,@DestinationPort,@DestinationIsland,@EstimatedDurationMinutes,@Fare,@VesselType,@OperatingHours,@OperatorNotes,@IsActive,@SourceUrl,@VerifiedAt,@ValidUntil,@FareValidUntil,@OperatorName,@IsAccessible);SELECT LAST_INSERT_ID()",route,tx,cancellationToken:ct));
        else await db.ExecuteAsync(new CommandDefinition("UPDATE mb_river_routes SET name=@Name,departure_port=@DeparturePort,destination_port=@DestinationPort,destination_island=@DestinationIsland,estimated_duration_minutes=@EstimatedDurationMinutes,fare=@Fare,vessel_type=@VesselType,operating_hours=@OperatingHours,operator_notes=@OperatorNotes,is_active=@IsActive,source_url=@SourceUrl,verified_at=@VerifiedAt,valid_until=@ValidUntil,fare_valid_until=@FareValidUntil,operator_name=@OperatorName,is_accessible=@IsAccessible,revision=revision+1 WHERE id=@Id",route,tx,cancellationToken:ct));
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_audit_logs(actor_id,action,entity_type,entity_id,details) VALUES(@actorId,'transit.river_saved','river_route',@Id,@details)",new{actorId,route.Id,details=System.Text.Json.JsonSerializer.Serialize(new{previous,route})},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);return true;
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
