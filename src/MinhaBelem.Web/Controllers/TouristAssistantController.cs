using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
namespace MinhaBelem.Web.Controllers;
[Route("guia-ia")]
public sealed class TouristAssistantController(ITouristAssistant assistant):Controller
{
    [HttpGet("")]
    public IActionResult Index()=>View(new TouristAssistantPage("",null));
    [HttpPost(""),ValidateAntiForgeryToken,Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("assistant")]
    public async Task<IActionResult> Search(string question,bool useAi,CancellationToken ct)
    {if(string.IsNullOrWhiteSpace(question)||question.Length is <3 or >500)return BadRequest();Response.Headers.CacheControl="no-store";return View("Index",new TouristAssistantPage(question,await assistant.SearchAsync(question,CultureInfo.CurrentUICulture.Name,useAi,ct)));}
}
public sealed record TouristAssistantPage(string Question,TouristAnswer? Answer);
