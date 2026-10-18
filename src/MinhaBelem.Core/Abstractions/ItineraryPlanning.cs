using MinhaBelem.Core.Entities;
namespace MinhaBelem.Core.Abstractions;
public sealed class ItineraryRequest
{
    public string Origin{get;set;}="";public DateTime LocalStart{get;set;}
    public decimal Days{get;set;}=1;public string Mode{get;set;}="transit";
    public string? Category{get;set;}public decimal? Budget{get;set;}
    public bool Children{get;set;}public bool Accessible{get;set;}public bool FreeOnly{get;set;}
    public int VisitMinutes{get;set;}=90;
    public int People {get;set;}=1;
    public int? AvailableMinutes {get;set;}
}
public sealed record PlannedVisit(Place Place,DateTime ArrivalUtc,DateTime DepartureUtc,RouteOption Route,decimal? EntryPrice,string SourceUrl);
public sealed record PlannedDay(DateTime LocalDate,IReadOnlyList<PlannedVisit> Visits,RoutePlan? ReturnRoute,decimal KnownCost,bool CostComplete)
{
    public bool ReturnTimeConfirmed {get;init;}
}
public sealed record PlannedItinerary(IReadOnlyList<PlannedDay> Days,string State);
public interface IItineraryPlanning{Task<PlannedItinerary> PlanAsync(ItineraryRequest request,string culture,CancellationToken ct);}
