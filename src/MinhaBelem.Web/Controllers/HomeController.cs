using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Text;
using TaxonomyInfo = MinhaBelem.Core.Taxonomy.Taxonomy;
namespace MinhaBelem.Web.Controllers;

public sealed class HomeController(IContentRepository content, ICommunityRepository community) : Controller
{
    public async Task<IActionResult> Index(string? categoria, string? busca, string? bairro, string? municipio, string? assunto, bool gratuito = false, DateOnly? de = null, DateOnly? ate = null, int pagina = 1, CancellationToken ct = default)
    {
        if (categoria?.Length > 60 || busca?.Length > 120 || bairro?.Length > 120 || municipio?.Length > 120 || assunto?.Length > 64 || pagina < 1 || pagina > 10000) return BadRequest();
        var category = TaxonomyInfo.Resolve(categoria);
        if (categoria is not null && category is null)
        {
            var subject = TaxonomyInfo.SubjectByKey(categoria);
            if (subject is null && categoria != "eventos") return BadRequest("Categoria inválida.");
            category = subject?.Category; assunto = subject?.Key;
        }
        ViewData["Category"] = categoria; ViewData["Search"] = busca; ViewData["Page"] = pagina;
        return View(await content.SearchAsync(new ContentQuery { Category = category, Search = busca, Subject = assunto, Neighborhood = bairro, Municipality = municipio, FreeOnly = gratuito, DateFrom = de, DateTo = ate, OnlyUpcomingEvents = categoria == "eventos", Page = pagina }, ct));
    }
    public async Task<IActionResult> Detalhe(long id, CancellationToken ct)
    {
        var item = await content.GetAsync(id, ct);
        return item is null || !item.IsVisibleToPublic(DateTime.UtcNow) ? NotFound() : View(item);
    }
    [HttpPost, EnableRateLimiting("submission")]
    public async Task<IActionResult> Colaborar(string nome, string mensagem, bool consentimento, CancellationToken ct)
    {
        if (!consentimento || string.IsNullOrWhiteSpace(nome) || nome.Length > 100 || string.IsNullOrWhiteSpace(mensagem) || mensagem.Length > 2500)
        { TempData["Notice"] = "Confira os campos e o aviso de privacidade."; return RedirectToAction(nameof(Index)); }
        await community.InsertAsync(new CommunitySubmission { DisplayName = HtmlText.Clean(nome, 100), Message = HtmlText.ToPlainText(mensagem, 2500) }, ct);
        TempData["Notice"] = "Sugestão enviada para revisão. Ela não será publicada automaticamente.";
        return RedirectToAction(nameof(Index));
    }
    public IActionResult Error() { ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier; return View(); }
    [HttpGet] public IActionResult Status(int code) { Response.StatusCode = code; ViewData["Code"] = code; return View(); }
    [HttpGet("privacidade")] public IActionResult Privacidade() => View();
    [HttpGet("termos")] public IActionResult Termos() => View();
    [HttpGet("robots.txt")] public IActionResult Robots() => Content("User-agent: *\nDisallow: /admin\nDisallow: /conta\nDisallow: /editorial\n", "text/plain");
}
