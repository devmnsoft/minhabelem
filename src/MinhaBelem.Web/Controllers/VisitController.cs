using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Policies;
using MinhaBelem.Web.Models;
namespace MinhaBelem.Web.Controllers;
// Uses the existing transit catalog, alerts and places; routing is a provider, not another catalog.
[Route("mobilidade")]
public sealed class VisitController(IRoutePlanner planner,IPlaceRepository places,IVisitRepository visits,ITransitRepository transit):Controller
{
    [HttpGet("como-chegar"),HttpGet("/como-chegar")]
    public async Task<IActionResult> Index(long? placeId=null,string? destino=null,DateTime? quando=null,string? modo=null,CancellationToken ct=default)
    {
        if(modo is not(null or "transit" or "walking" or "bicycling" or "driving" or "boat"))return BadRequest();
        return await RenderAsync(new(){PlaceId=placeId,Destination=destino??"",LocalTime=quando,Mode=modo??"transit"},false,0,null,ct);
    }
    [HttpPost("como-chegar"),ValidateAntiForgeryToken,Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("routing")]
    public async Task<IActionResult> Calculate(RouteQuery query,decimal optional=0,decimal? inbound=null,CancellationToken ct=default)
    {if(!ModelState.IsValid)return BadRequest();try{return await RenderAsync(query,true,optional,inbound,ct);}catch(ArgumentException){return BadRequest("Confira origem, destino, data e custos informados.");}}
    private async Task<IActionResult> RenderAsync(RouteQuery query,bool calculate,decimal optional,decimal? inbound,CancellationToken ct)
    {
        Response.Headers.CacheControl="private,no-store";Response.Headers["Referrer-Policy"]="no-referrer";
        query.Culture=CultureInfo.CurrentUICulture.Name;
        Place? place=null;VisitSchedule schedule=new(null,[]);
        if(query.PlaceId is {} id){place=await places.GetAsync(id,ct);if(place is null||!place.IsPublished||place.IsStale(DateTime.UtcNow))return NotFound();query.Destination=place.Name+", "+place.FormattedAddress+", "+place.Municipality;schedule=await visits.GetAsync(id,ct);}
        if(query.Destination.Length>350||query.Origin.Length>350||optional is <0 or >100000||inbound is <0 or >100000)return BadRequest();
        var plan=calculate?await planner.PlanAsync(query,ct):query.Mode=="boat"
            ?new RoutePlan("unavailable","SEGBEL","Consulta fluvial sem itinerário digital confirmado.",null,[],"https://seguranca.belem.pa.gov.br/")
            :new RoutePlan("idle","—","Informe origem e destino para consultar opções verificáveis.",null,[],"https://www.google.com/maps/dir/?api=1&destination="+Uri.EscapeDataString(query.Destination));
        var time=query.LocalTime is {} wall?PortalTime.Utc(wall):DateTime.UtcNow;
        if(!query.ArriveBy&&plan.Options.FirstOrDefault() is {} option)time=time.AddSeconds(option.DurationSeconds);
        var assessment=VisitRecommendationService.Evaluate(schedule,time,DateTime.UtcNow);
        var validPrice=schedule.Info is {} info&&info.ConfirmedAt<=DateTime.UtcNow&&info.ValidUntil>time&&info.ValidUntil>DateTime.UtcNow?info.EntryPrice:null;
        var outbound=plan.Options.FirstOrDefault() is {Currency:"BRL"} route?route.Fare:null;
        var costs=VisitRecommendationService.Cost(outbound,validPrice,inbound,optional);
        ViewData["Places"]=await places.SearchAsync(new PlaceQuery{PublishedOnly=true,PageSize=30,Culture=CultureInfo.CurrentUICulture.Name},ct);
        return View("Index",new VisitPage(query,place,plan,schedule,assessment,validPrice,costs.KnownSubtotal,costs.Total,inbound,optional,await transit.GetActiveAlertsAsync(ct)));
    }
}
public sealed record VisitPage(RouteQuery Query,Place? Place,RoutePlan Plan,VisitSchedule Schedule,VisitAssessment Assessment,decimal? EntryPrice,decimal KnownSubtotal,decimal? Total,decimal? Inbound,decimal Optional,IReadOnlyList<TransitAlert> Alerts);
