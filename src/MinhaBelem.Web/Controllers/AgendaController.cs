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
    [HttpGet("agenda/eventos/{id:long}/calendario.ics")]
    public async Task<IActionResult> Calendar(long id, CancellationToken ct)
    {
        var item = await content.GetAsync(id, ct);
        if (item is null || !item.IsVisibleToPublic(DateTime.UtcNow) || item.Kind != Core.Entities.ContentKind.Event || item.EventStartAt is not {} start) return NotFound();
        static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n").Replace(";", "\\;").Replace(",", "\\,");
        var lines = new List<string> { "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//MinhaBelem//Agenda//PT", "CALSCALE:GREGORIAN", "BEGIN:VEVENT", $"UID:content-{id}@minhabelem", "DTSTAMP:" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"), "SUMMARY:" + Escape(item.DisplayTitle), "DESCRIPTION:" + Escape(item.DisplaySummary + "\n" + item.SourceUrl), "LOCATION:" + Escape(item.Address ?? item.Municipality ?? "Belém"), "URL:" + item.SourceUrl, "STATUS:" + (item.Status == Core.Entities.ContentStatus.Cancelled ? "CANCELLED" : "CONFIRMED") };
        if (AgendaPage.DateOnlyEvent(item))
        {
            lines.Add("DTSTART;VALUE=DATE:" + PortalTime.Local(start).ToString("yyyyMMdd"));
            lines.Add("DTEND;VALUE=DATE:" + PortalTime.Local(item.EventEndAt ?? start).Date.AddDays(1).ToString("yyyyMMdd"));
        }
        else
        {
            lines.Add("DTSTART:" + start.ToString("yyyyMMdd'T'HHmmss'Z'"));
            if (item.EventEndAt is {} end) lines.Add("DTEND:" + end.ToString("yyyyMMdd'T'HHmmss'Z'"));
        }
        lines.Add("END:VEVENT"); lines.Add("END:VCALENDAR");
        var output = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            var bytes = 0;
            foreach (var rune in line.EnumerateRunes())
            {
                if (bytes + rune.Utf8SequenceLength > 73) { output.Append("\r\n "); bytes = 1; }
                output.Append(rune.ToString()); bytes += rune.Utf8SequenceLength;
            }
            output.Append("\r\n");
        }
        return File(System.Text.Encoding.UTF8.GetBytes(output.ToString()), "text/calendar; charset=utf-8", $"minhabelem-evento-{id}.ics");
    }
    [HttpGet("agenda"), HttpGet("{culture:regex(^(pt-BR|en|fr|es|ru|zh-Hans|ja)$)}/agenda"), HttpGet("agenda/semana"), HttpGet("proximos-eventos"), HttpGet("programacao-infantil"), HttpGet("cinema-teatro"), HttpGet("esporte-lazer"), HttpGet("eventos-gratuitos"), HttpGet("agenda/meu-bairro"), HttpGet("fim-de-semana"), HttpGet("eventos-do-para"), HttpGet("feriados"), HttpGet("agora")]
    public async Task<IActionResult> Index(int dias = 365, string? busca = null, string? bairro = null, string? municipio = null, string? categoria = null, Audience? publico = null, AccessibilityFeature? acessibilidade = null, int? idade = null, bool gratuito = false, DateOnly? data = null, DateOnly? mes = null, string? tipo = null, CancellationToken ct = default)
    {
        if (!new[] { 0, 1, 7, 30, 90, 180, 365 }.Contains(dias) || busca?.Length > 120 || bairro?.Length > 120 || municipio?.Length > 120 || idade is < 0 or > 18 || publico is { } p && !Enum.IsDefined(p) || acessibilidade is { } a && !Enum.IsDefined(a)) return BadRequest();
        var today = PortalTime.Local(DateTime.UtcNow).Date;
        var from = today; var until = today.AddDays(dias == 0 ? 1 : dias == 1 ? 2 : dias + 1);
        if (tipo is not null && !MinhaBelem.Infrastructure.MySql.CuratedAgendaImporter.AgendaSubjects.Contains(tipo)) return BadRequest();
        if (dias == 1) from = today.AddDays(1);
        var currentCulture = CultureInfo.CurrentUICulture.Name;
        var query = new ContentQuery { Search = busca, Neighborhood = bairro, Municipality = municipio, Category = Core.Taxonomy.Taxonomy.Resolve(categoria), Subject = tipo, Audience = publico, Accessibility = acessibilidade, AgeAtMost = idade, FreeOnly = gratuito, OnlyUpcomingEvents = true, StartsFromUtc = PortalTime.Utc(from), StartsBeforeUtc = PortalTime.Utc(until), PageSize = 500, Sort = ContentSort.Upcoming, Culture = currentCulture };
        var path = Request.Path.Value;
        query.EventsOverlapWindow = true;
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
        if (mes is { } month)
        {
            if(month.Year is <2000 or >2099)return BadRequest();
            var first=new DateOnly(month.Year,month.Month,1);
            query.StartsFromUtc=PortalTime.Utc(first.ToDateTime(TimeOnly.MinValue));
            query.StartsBeforeUtc=PortalTime.Utc(first.AddMonths(1).ToDateTime(TimeOnly.MinValue));
            query.OnlyUpcomingEvents=true;items=await content.SearchAsync(query,ct);
        }
        var visibleMonth = mes ?? data ?? DateOnly.FromDateTime(today);
        var monthStart = new DateOnly(visibleMonth.Year, visibleMonth.Month, 1);
        query.StartsFromUtc = PortalTime.Utc(monthStart.ToDateTime(TimeOnly.MinValue));
        query.StartsBeforeUtc = PortalTime.Utc(monthStart.AddMonths(1).ToDateTime(TimeOnly.MinValue));
        var calendarItems = await content.SearchAsync(query, ct);
        return View(new AgendaPage(title, loc["AgendaDescription"], items, dias) { SelectedDate = data,Month=visibleMonth,CalendarItems=calendarItems });
    }
}
