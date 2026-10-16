using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Web.Models;
namespace MinhaBelem.Web.Controllers;
public sealed class AgendaController(IContentRepository content) : Controller
{
    [HttpGet("agenda"), HttpGet("agenda/semana"), HttpGet("proximos-eventos"), HttpGet("programacao-infantil"), HttpGet("cinema-teatro"), HttpGet("esporte-lazer"), HttpGet("eventos-gratuitos"), HttpGet("meu-bairro"), HttpGet("fim-de-semana"), HttpGet("eventos-do-para"), HttpGet("feriados"), HttpGet("agora")]
    public async Task<IActionResult> Index(int dias = 30, string? busca = null, string? bairro = null, string? municipio = null, string? categoria = null, Audience? publico = null, AccessibilityFeature? acessibilidade = null, int? idade = null, bool gratuito = false, CancellationToken ct = default)
    {
        if (!new[] { 0, 1, 7, 30, 90, 180, 365 }.Contains(dias) || busca?.Length > 120 || bairro?.Length > 120 || municipio?.Length > 120 || idade is < 0 or > 18 || publico is { } p && !Enum.IsDefined(p) || acessibilidade is { } a && !Enum.IsDefined(a)) return BadRequest();
        var today = PortalTime.Local(DateTime.UtcNow).Date;
        var from = today; var until = today.AddDays(dias == 0 ? 1 : dias == 1 ? 2 : dias + 1);
        if (dias == 1) from = today.AddDays(1);
        var query = new ContentQuery { Search = busca, Neighborhood = bairro, Municipality = municipio, Category = Core.Taxonomy.Taxonomy.Resolve(categoria), Audience = publico, Accessibility = acessibilidade, AgeAtMost = idade, FreeOnly = gratuito, OnlyUpcomingEvents = true, StartsFromUtc = PortalTime.Utc(from), StartsBeforeUtc = PortalTime.Utc(until), PageSize = 100, Sort = ContentSort.Upcoming };
        var path = Request.Path.Value;
        var title = "Próximos eventos";
        if (path == "/agenda/semana") { title = "Agenda da semana"; query.StartsBeforeUtc = PortalTime.Utc(today.AddDays(7)); }
        if (path == "/programacao-infantil") { title = "Programação infantil"; query.Audience = Audience.Infantil; }
        if (path == "/cinema-teatro") { title = "Cinema e teatro"; query.Subjects = new[] { "cinema", "teatro" }; }
        if (path == "/esporte-lazer") { title = "Esporte e lazer"; query.Category = ContentCategory.Esporte; }
        if (path == "/eventos-gratuitos") { title = "Eventos gratuitos"; query.FreeOnly = true; }
        if (path == "/meu-bairro") title = "Meu bairro";
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
        var items = await content.SearchAsync(query, ct);
        return View(new AgendaPage(title, "Programações com fonte identificada e revisão editorial. Os horários seguem o fuso de Belém.", items, dias));
    }
}
