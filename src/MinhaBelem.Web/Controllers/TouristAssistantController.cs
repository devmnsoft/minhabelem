using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;

namespace MinhaBelem.Web.Controllers;

[Route("guia-ia")]
public sealed class TouristAssistantController(ITouristAssistant assistant) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? q, [FromQuery] bool useAi = true, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(q) && q.Length is >= 3 and <= 500)
        {
            Response.Headers.CacheControl = "no-store";
            var answer = await assistant.SearchAsync(q, CultureInfo.CurrentUICulture.Name, useAi, ct);
            return View(new TouristAssistantPage(q, answer));
        }
        return View(new TouristAssistantPage("", null));
    }

    [HttpPost(""), ValidateAntiForgeryToken, Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("assistant")]
    public async Task<IActionResult> Search(string question, bool useAi = true, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length is < 3 or > 500) return BadRequest();
        Response.Headers.CacheControl = "no-store";
        var answer = await assistant.SearchAsync(question, CultureInfo.CurrentUICulture.Name, useAi, ct);
        return View("Index", new TouristAssistantPage(question, answer));
    }
}

public sealed record TouristAssistantPage(string Question, TouristAnswer? Answer);
