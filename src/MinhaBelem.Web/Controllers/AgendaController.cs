using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Web.Models;
using Microsoft.Extensions.Localization;
using MinhaBelem.Web.Resources;
namespace MinhaBelem.Web.Controllers;
public sealed class AgendaController(IContentRepository content, IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("agenda"), HttpGet("{culture:regex(^(pt-BR|en|fr|es|ru|zh-Hans|ja)$)}/agenda"), HttpGet("agenda/semana"), HttpGet("proximos-eventos"), HttpGet("programacao-infantil"), HttpGet("cinema-teatro"), HttpGet("esporte-lazer"), HttpGet("eventos-gratuitos"), HttpGet("agenda/meu-bairro"), HttpGet("fim-de-semana"), HttpGet("eventos-do-para"), HttpGet("feriados"), HttpGet("agora")]
    public async Task<IActionResult> Index(int dias = 30, string? busca = null, string? bairro = null, string? municipio = null, string? categoria = null, Audience? publico = null, AccessibilityFeature? acessibilidade = null, int? idade = null, bool gratuito = false, DateOnly? data = null, CancellationToken ct = default)
    {
        if (!new[] { 0, 1, 7, 30, 90, 180, 365 }.Contains(dias) || busca?.Length > 120 || bairro?.Length > 120 || municipio?.Length > 120 || idade is < 0 or > 18 || publico is { } p && !Enum.IsDefined(p) || acessibilidade is { } a && !Enum.IsDefined(a)) return BadRequest();
        var today = PortalTime.Local(DateTime.UtcNow).Date;
        var from = today; var until = today.AddDays(dias == 0 ? 1 : dias == 1 ? 2 : dias + 1);
        if (dias == 1) from = today.AddDays(1);
        var currentCulture = CultureInfo.CurrentUICulture.Name;
        var query = new ContentQuery { Search = busca, Neighborhood = bairro, Municipality = municipio, Category = Core.Taxonomy.Taxonomy.Resolve(categoria), Audience = publico, Accessibility = acessibilidade, AgeAtMost = idade, FreeOnly = gratuito, OnlyUpcomingEvents = true, StartsFromUtc = PortalTime.Utc(from), StartsBeforeUtc = PortalTime.Utc(until), PageSize = 100, Sort = ContentSort.Upcoming, Culture = currentCulture };
        var path = Request.Path.Value;
        var title = "Próximos eventos";
        if (path == "/agenda/semana") { title = "Agenda da semana"; query.StartsBeforeUtc = PortalTime.Utc(today.AddDays(7)); }
        if (path == "/programacao-infantil") { title = "Programação infantil"; query.Audience = Audience.Infantil; }
        if (path == "/cinema-teatro") { title = "Cinema e teatro"; query.Subjects = new[] { "cinema", "teatro" }; }
        if (path == "/esporte-lazer") { title = "Esporte e lazer"; query.Category = ContentCategory.Esporte; }
        if (path == "/eventos-gratuitos") { title = "Eventos gratuitos"; query.FreeOnly = true; }
        if (path is "/meu-bairro" or "/agenda/meu-bairro") title = "Meu bairro";
        if (path == "/eventos-do-para") title = "Eventos do Pará";
        if (path == "/fim-de-semana")
        {
            title = "O que fazer neste fim de semana";
            var saturday = today.AddDays(((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7);
            if (today.DayOfWeek == DayOfWeek.Sunday) saturday = today.AddDays(-1);
            query.StartsFromUtc = PortalTime.Utc(saturday); query.StartsBeforeUtc = PortalTime.Utc(saturday.AddDays(2));
        }
        if (path == "/feriados") { title = "Feriados e datas comemorativas"; query.Subject = "feriados"; query.OnlyUpcomingEvents = false; query.StartsFromUtc = null; query.StartsBeforeUtc = null; }
        if (path == "/agora") { title = "MinhaBelém agora"; query.OnlyUpcomingEvents = false; query.StartsFromUtc = null; query.StartsBeforeUtc = null; query.Sort = ContentSort.Recent; }
        var titleKey = path switch
        {
            "/agenda/semana" => "ThisWeek", "/programacao-infantil" => "ChildrenProgram", "/cinema-teatro" => "CinemaTheater",
            "/esporte-lazer" => "Category_esporte", "/eventos-gratuitos" => "FreeEvents", "/agenda/meu-bairro" => "NavNeighborhood",
            "/fim-de-semana" => "ThisWeekend", "/agora" => "CityNow", _ => "UpcomingEvents"
        };
        if (path is not ("/feriados" or "/eventos-do-para")) title = loc[titleKey];
        if (data is { } selected && query.OnlyUpcomingEvents)
        {
            if (selected.Year is < 2000 or > 2100) return BadRequest();
            query.StartsFromUtc = PortalTime.Utc(selected.ToDateTime(TimeOnly.MinValue));
            query.StartsBeforeUtc = PortalTime.Utc(selected.AddDays(1).ToDateTime(TimeOnly.MinValue));
        }
        var items = await content.SearchAsync(query, ct);
        return View(new AgendaPage(title, loc["AgendaDescription"], items, dias) { SelectedDate = data });
    }
}
