using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
namespace MinhaBelem.Web.Controllers;
[Route("explore/planejar")]
public sealed class ItineraryPlanningController(IItineraryPlanning planning):Controller
{
    [HttpGet("")]public IActionResult Index()=>View(new ItineraryPlanningPage(new(){LocalStart=MinhaBelem.Web.Models.PortalTime.Local(DateTime.UtcNow).Date.AddDays(1).AddHours(9)},null));
    [HttpPost(""),ValidateAntiForgeryToken,Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("itinerary")]public async Task<IActionResult> Generate(ItineraryRequest request,CancellationToken ct)
    {if(!ModelState.IsValid)return BadRequest();try{Response.Headers.CacheControl="no-store";return View("Index",new ItineraryPlanningPage(request,await planning.PlanAsync(request,CultureInfo.CurrentUICulture.Name,ct)));}catch(ArgumentException){return BadRequest("Confira origem, datas e filtros do roteiro.");}}
}
public sealed record ItineraryPlanningPage(ItineraryRequest Request,PlannedItinerary? Result);
