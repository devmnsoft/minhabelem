using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Policies;
using MinhaBelem.Core.Taxonomy;
namespace MinhaBelem.Infrastructure.Places;
/// <summary>Only visits with current opening evidence and a provider-calculated incoming leg enter the result.</summary>
public sealed class ItineraryPlanning(IPlaceRepository places,IVisitRepository visits,IRoutePlanner routes):IItineraryPlanning
{
    public async Task<PlannedItinerary> PlanAsync(ItineraryRequest q,string culture,CancellationToken ct)
    {
        var now=DateTime.UtcNow;var start=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(q.LocalStart,DateTimeKind.Unspecified),VisitRecommendationService.Zone);
        if(q.Origin.Length is <3 or >350||q.Days is not(.5m or 1m or 2m or 3m or 5m)||q.VisitMinutes is <30 or >240||q.Budget<0||start<now.AddMinutes(-5)||start>now.AddDays(365)||q.Mode is not("transit" or "walking" or "bicycling" or "driving" or "boat")||q.Category is not null&&!PlacesCatalog.IsAllowedType(q.Category))throw new ArgumentException("Confira origem, período, orçamento e preferências.");
        var all=await places.SearchAsync(new PlaceQuery{PublishedOnly=true,PageSize=40,Culture=culture,Category=q.Category,Audience=q.Children?Audience.Infantil:null},ct);
        var candidates=all.Where(p=>p.Latitude is >=-90 and <=90&&p.Longitude is >=-180 and <=180&&(!q.Accessible||p.WheelchairAccessible==true)).ToList();
        var result=new List<PlannedDay>();double? latitude=null,longitude=null;decimal budgetUsed=0;
        for(var day=0;day<(int)Math.Ceiling(q.Days);day++)
        {
            var cursor=start.AddDays(day);var end=cursor.AddHours(q.Days==.5m?4:8);var origin=q.Origin;
            var stops=new List<PlannedVisit>();decimal known=0;var complete=true;
            var ordered=latitude is {} lat&&longitude is {} lng?candidates.OrderBy(p=>Math.Pow(p.Latitude!.Value-lat,2)+Math.Pow(p.Longitude!.Value-lng,2)).ToArray():candidates.ToArray();
            foreach(var place in ordered.Take(8))
            {
                if(stops.Count>=3||cursor.AddMinutes(q.VisitMinutes)>end)break;
                var schedule=await visits.GetAsync(place.Id,ct);var info=schedule.Info;
                if(info is null||VisitRecommendationService.Evaluate(schedule,cursor,now).State!="open"||q.FreeOnly&&info.EntryPrice!=0)continue;
                var destination=place.Name+", "+place.FormattedAddress+", "+place.Municipality;
                var plan=await routes.PlanAsync(new(){Origin=origin,Destination=destination,LocalTime=TimeZoneInfo.ConvertTimeFromUtc(cursor,VisitRecommendationService.Zone),Mode=q.Mode,Accessible=q.Accessible},ct);
                if(plan.State is "unconfigured" or "budget" or "error")break;
                var route=plan.Options.OrderBy(r=>r.DurationSeconds).FirstOrDefault();if(route is null)continue;
                var arrive=cursor.AddSeconds(route.DurationSeconds);var depart=arrive.AddMinutes(q.VisitMinutes);var assessment=VisitRecommendationService.Evaluate(schedule,arrive,now);
                if(assessment.State!="open"||depart>end||assessment.NextChangeUtc<depart)continue;
                var fare=route.Currency=="BRL"?route.Fare:null;
                if(q.Budget.HasValue&&(!fare.HasValue||!info.EntryPrice.HasValue||budgetUsed+known+fare.Value+info.EntryPrice.Value>q.Budget))continue;
                known+=(fare??0)+(info.EntryPrice??0);complete&=fare.HasValue&&info.EntryPrice.HasValue;
                stops.Add(new(place,arrive,depart,route,info.EntryPrice,info.SourceUrl));candidates.Remove(place);
                latitude=place.Latitude;longitude=place.Longitude;origin=destination;cursor=depart;
            }
            RoutePlan? returnRoute=null;
            if(stops.Count>0)
            {
                returnRoute=await routes.PlanAsync(new(){Origin=origin,Destination=q.Origin,LocalTime=TimeZoneInfo.ConvertTimeFromUtc(cursor,VisitRecommendationService.Zone),Mode=q.Mode,Accessible=q.Accessible},ct);
                var fare=returnRoute.Options.FirstOrDefault() is {Currency:"BRL"} back?back.Fare:null;known+=fare??0;complete&=fare.HasValue;
                // A limited budget is never declared satisfied when return fare is unknown or exceeds it.
                if(q.Budget.HasValue&&(!complete||budgetUsed+known>q.Budget)){result.Add(new(q.LocalStart.AddDays(day).Date,[],returnRoute,known,false));continue;}
            }
            budgetUsed+=known;result.Add(new(q.LocalStart.AddDays(day).Date,stops,returnRoute,known,complete&&stops.Count>0));
        }
        return new(result,result.Any(d=>d.Visits.Count>0)?"calculated":"unavailable");
    }
}
