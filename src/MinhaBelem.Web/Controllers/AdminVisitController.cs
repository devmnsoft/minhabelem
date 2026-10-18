using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Taxonomy;
namespace MinhaBelem.Web.Controllers;
[Route("admin/lugares/{id:long}/visita"),Authorize(Policy=MinhaBelem.Core.Security.Permissions.PlaceManage)]
public sealed class AdminVisitController(IVisitRepository visits,IPlaceRepository places):Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(long id,CancellationToken ct)
    {var place=await places.GetAsync(id,ct);if(place is null)return NotFound();ViewData["Place"]=place;return View(await visits.GetAsync(id,ct));}
    [HttpPost("")]
    public async Task<IActionResult> Save(long id,PlaceVisitInfo info,VisitDayInput[] days,int revision,CancellationToken ct)
    {
        try
        {
            if(!ModelState.IsValid)return BadRequest();
            if(days.Length>380)return BadRequest();
            var hours=new List<VisitHours>();
            foreach(var d in days)
            {
                if(d.Weekday==-1&&d.Date is null)continue;
                var weekday=d.Date is {} date?(int)date.DayOfWeek:d.Weekday;
                if(d.Closed||d.AllDay){hours.Add(new(){Weekday=weekday,SpecialDate=d.Date,Sector=d.Sector,IsClosed=d.Closed,Is24h=d.AllDay});continue;}
                foreach(var segment in (d.Intervals??"").Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries))
                {var pair=segment.Split('-');if(pair.Length!=2||!TimeOnly.TryParseExact(pair[0],"HH:mm",out var start)||!TimeOnly.TryParseExact(pair[1],"HH:mm",out var end))return BadRequest("Use HH:mm-HH:mm;HH:mm-HH:mm.");hours.Add(new(){Weekday=weekday,SpecialDate=d.Date,Sector=d.Sector,OpenMinute=start.Hour*60+start.Minute,CloseMinute=end.Hour*60+end.Minute});}
            }
            info.PlaceId=id;info.ConfirmedAt=DateTime.SpecifyKind(info.ConfirmedAt,DateTimeKind.Utc);info.ValidUntil=DateTime.SpecifyKind(info.ValidUntil,DateTimeKind.Utc);
            return await visits.SaveAsync(info,hours,revision,long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),ct)?RedirectToAction(nameof(Index),new{id}):Conflict("Outro editor alterou estes dados. Recarregue a página.");
        }
        catch(ArgumentException){return BadRequest("Confira fonte, validade, turnos e preços.");}
    }
}
public sealed class VisitDayInput{public int Weekday{get;set;}public DateTime? Date{get;set;}public string Sector{get;set;}="principal";public string? Intervals{get;set;}public bool Closed{get;set;}public bool AllDay{get;set;}}
