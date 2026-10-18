namespace MinhaBelem.Core.Abstractions;
public sealed class RouteQuery
{
    public string Origin {get;set;}="";
    public string Destination {get;set;}="";
    public long? PlaceId {get;set;}
    public DateTime? LocalTime {get;set;}
    public bool ArriveBy {get;set;}
    public string Mode {get;set;}="transit";
    public string Preference {get;set;}="less_walking";
    public int MaxWalkMeters {get;set;}=1000;
    public bool Accessible {get;set;}
    public string Culture {get;set;}="pt-BR";
}
public sealed record RouteStep(string Mode,string Instruction,int? DistanceMeters,string? Line,string? Board,string? Alight,DateTimeOffset? Departure,DateTimeOffset? Arrival);
public sealed record RouteOption(string Description,int DistanceMeters,int DurationSeconds,IReadOnlyList<RouteStep> Steps,decimal? Fare,string? Currency,string? Polyline);
public sealed record RoutePlan(string State,string Provider,string Message,DateTime? QueriedAt,IReadOnlyList<RouteOption> Options,string ExternalUrl);
public interface IRoutePlanner { Task<RoutePlan> PlanAsync(RouteQuery query,CancellationToken ct); }
public sealed class PlaceVisitInfo
{
    public long PlaceId{get;set;}public int Revision{get;set;}public string SourceUrl{get;set;}="";
    public DateTime ConfirmedAt{get;set;}public DateTime ValidUntil{get;set;}public DateTime? ClosedUntil{get;set;}
    public bool ReservationOnly{get;set;}public int? LastEntryMinutes{get;set;}public decimal? EntryPrice{get;set;}public decimal? HalfPrice{get;set;}public string? PriceConditions{get;set;}
}
public sealed class VisitHours
{
    public int Weekday{get;set;}public DateTime? SpecialDate{get;set;}public string Sector{get;set;}="principal";
    public int? OpenMinute{get;set;}public int? CloseMinute{get;set;}public bool IsClosed{get;set;}public bool Is24h{get;set;}
}
public sealed record VisitSchedule(PlaceVisitInfo? Info,IReadOnlyList<VisitHours> Hours);
public sealed record VisitAssessment(string State,string Confidence,string Reason,DateTime? NextChangeUtc=null);
public interface IVisitRepository
{
    Task<VisitSchedule> GetAsync(long placeId,CancellationToken ct);
    Task<bool> SaveAsync(PlaceVisitInfo info,IReadOnlyList<VisitHours> hours,int expectedRevision,long actorId,CancellationToken ct);
}
