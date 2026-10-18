using System.Text.Json;
using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Security;
namespace MinhaBelem.Infrastructure.MySql;
public sealed class VisitRepository(MySqlDataSource data):IVisitRepository
{
    public async Task<VisitSchedule> GetAsync(long placeId,CancellationToken ct)
    {
        await using var db=await data.OpenConnectionAsync(ct);
        var info=await db.QuerySingleOrDefaultAsync<PlaceVisitInfo>(new CommandDefinition("SELECT place_id AS PlaceId,revision,source_url AS SourceUrl,confirmed_at AS ConfirmedAt,valid_until AS ValidUntil,closed_until AS ClosedUntil,reservation_only AS ReservationOnly,last_entry_minutes AS LastEntryMinutes,entry_price AS EntryPrice,half_price AS HalfPrice,price_conditions AS PriceConditions FROM mb_place_visit_info WHERE place_id=@placeId",new{placeId},cancellationToken:ct));
        var hours=(await db.QueryAsync<VisitHours>(new CommandDefinition("SELECT weekday,special_date AS SpecialDate,sector,open_minute AS OpenMinute,close_minute AS CloseMinute,is_closed AS IsClosed,is_24h AS Is24h FROM mb_place_visit_hours WHERE place_id=@placeId ORDER BY special_date,weekday,open_minute",new{placeId},cancellationToken:ct))).AsList();
        return new(info,hours);
    }
    public async Task<bool> SaveAsync(PlaceVisitInfo info,IReadOnlyList<VisitHours> hours,int expectedRevision,long actorId,CancellationToken ct)
    {
        if(!SafeUrl.Validate(info.SourceUrl).IsValid||info.SourceUrl.Length>2000||info.ConfirmedAt>DateTime.UtcNow||info.ValidUntil<=DateTime.UtcNow||info.ValidUntil>info.ConfirmedAt.AddDays(366)||info.EntryPrice<0||info.HalfPrice<0||info.PriceConditions?.Length>500||info.LastEntryMinutes is <0 or >1439)throw new ArgumentException("Confira fonte, validade, preços e última entrada.");
        if(hours.Count is 0 or >400||hours.Any(h=>h.Weekday is <0 or >6||h.Sector.Length is <1 or >120||h.OpenMinute is <0 or >1439||h.CloseMinute is <0 or >1439||!h.IsClosed&&!h.Is24h&&(h.OpenMinute is null||h.CloseMinute is null||h.OpenMinute==h.CloseMinute)))throw new ArgumentException("Confira dias, setores e turnos.");
        foreach(var group in hours.GroupBy(h=>(h.Sector,h.SpecialDate?.Date,h.Weekday)))
        {
            if(group.Any(h=>h.IsClosed||h.Is24h)&&group.Count()>1)throw new ArgumentException("Não combine fechado/24h com outros turnos no mesmo dia.");
            var covered=new HashSet<int>();foreach(var h in group.Where(h=>!h.IsClosed&&!h.Is24h))for(var m=h.OpenMinute!.Value;m<(h.CloseMinute>h.OpenMinute?h.CloseMinute:h.CloseMinute+1440);m++)if(!covered.Add(m%1440))throw new ArgumentException("Turnos sobrepostos.");
        }
        await using var db=await data.OpenConnectionAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);
        if(await db.ExecuteScalarAsync<long?>(new CommandDefinition("SELECT id FROM mb_places WHERE id=@PlaceId FOR UPDATE",info,tx,cancellationToken:ct)) is null)return false;
        var current=await db.QuerySingleOrDefaultAsync(new CommandDefinition("SELECT * FROM mb_place_visit_info WHERE place_id=@PlaceId FOR UPDATE",info,tx,cancellationToken:ct));
        if((current is null?0:(int)current.revision)!=expectedRevision)return false;
        var before=(await db.QueryAsync(new CommandDefinition("SELECT * FROM mb_place_visit_hours WHERE place_id=@PlaceId",info,tx,cancellationToken:ct))).ToArray();
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_place_visit_history(place_id,actor_id,snapshot) VALUES(@placeId,@actorId,@snapshot)",new{placeId=info.PlaceId,actorId,snapshot=JsonSerializer.Serialize(new{previous=current,hours=before,next=info,nextHours=hours})},tx,cancellationToken:ct));
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_place_visit_info(place_id,source_url,confirmed_at,valid_until,closed_until,reservation_only,last_entry_minutes,entry_price,half_price,price_conditions) VALUES(@PlaceId,@SourceUrl,@ConfirmedAt,@ValidUntil,@ClosedUntil,@ReservationOnly,@LastEntryMinutes,@EntryPrice,@HalfPrice,@PriceConditions) ON DUPLICATE KEY UPDATE revision=revision+1,source_url=@SourceUrl,confirmed_at=@ConfirmedAt,valid_until=@ValidUntil,closed_until=@ClosedUntil,reservation_only=@ReservationOnly,last_entry_minutes=@LastEntryMinutes,entry_price=@EntryPrice,half_price=@HalfPrice,price_conditions=@PriceConditions",info,tx,cancellationToken:ct));
        await db.ExecuteAsync(new CommandDefinition("DELETE FROM mb_place_visit_hours WHERE place_id=@PlaceId",info,tx,cancellationToken:ct));
        foreach(var h in hours)await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_place_visit_hours(place_id,weekday,special_date,sector,open_minute,close_minute,is_closed,is_24h) VALUES(@PlaceId,@Weekday,@SpecialDate,@Sector,@OpenMinute,@CloseMinute,@IsClosed,@Is24h)",new{info.PlaceId,h.Weekday,h.SpecialDate,h.Sector,h.OpenMinute,h.CloseMinute,h.IsClosed,h.Is24h},tx,cancellationToken:ct));
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_audit_logs(actor_id,action,entity_type,entity_id,details) VALUES(@actorId,'place.visit_updated','place',@placeId,JSON_OBJECT('previousRevision',@expectedRevision))",new{actorId,placeId=info.PlaceId,expectedRevision},tx,cancellationToken:ct));
        await tx.CommitAsync(ct);return true;
    }
}
