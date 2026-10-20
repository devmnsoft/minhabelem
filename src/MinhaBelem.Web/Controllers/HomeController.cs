using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Text;
using MinhaBelem.Web.Resources;
using TaxonomyInfo = MinhaBelem.Core.Taxonomy.Taxonomy;

namespace MinhaBelem.Web.Controllers;

public sealed class HomeController(
    IContentRepository content,
    ICommunityRepository community,
    IPlaceRepository places,
    ITranslationRepository translationRepo,
    IAdvertiserRepository advertisers,
    ITransitRepository? transitRepo = null,
    INeighborhoodRepository? neighborhoodRepo = null,
    ITouristRepository? touristRepo = null,
    IRadarRepository? radarRepo = null) : Controller
{
    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("submission")]
    public async Task<IActionResult> Reportar(long id, string motivo, bool consentimento, CancellationToken ct)
    {
        var item = await content.GetAsync(id, ct);
        if (item is null || !item.IsVisibleToPublic(DateTime.UtcNow)) return NotFound();
        if (!consentimento || string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length is < 10 or > 1000) return BadRequest();
        await community.InsertAsync(new CommunitySubmission
        {
            DisplayName = "Relato sobre conteúdo #" + id,
            Message = "Conteúdo #" + id + "\n" + HtmlText.ToPlainText(motivo, 1000),
            Status = SubmissionStatus.Pending
        }, ct);
        TempData["Notice"] = LocalizationCatalog.Get("ReportReceived", CultureInfo.CurrentUICulture.Name);
        return RedirectToAction(nameof(Detalhe), new { id });
    }
    public async Task<IActionResult> Index(
        string? categoria,
        string? busca,
        string? bairro,
        string? municipio,
        string? assunto,
        bool gratuito = false,
        DateOnly? de = null,
        DateOnly? ate = null,
        int pagina = 1,
        CancellationToken ct = default)
    {
        if (categoria?.Length > 60 || busca?.Length > 120 || bairro?.Length > 120 || municipio?.Length > 120 || assunto?.Length > 64 || pagina < 1 || pagina > 10000) return BadRequest();
        var category = TaxonomyInfo.Resolve(categoria);
        if (categoria is not null && category is null)
        {
            var subject = TaxonomyInfo.SubjectByKey(categoria);
            if (subject is null && categoria != "eventos") return BadRequest("Categoria inválida.");
            category = subject?.Category; assunto = subject?.Key;
        }

        var currentCulture = CultureInfo.CurrentUICulture.Name;
        ViewData["Category"] = categoria;
        ViewData["Search"] = busca;
        ViewData["Page"] = pagina;
        ViewData["CurrentCulture"] = currentCulture;
        ViewData["Campaigns"] = await advertisers.ActiveAsync(DateTime.UtcNow, 3, ct);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            ViewData["Places"] = await places.SearchAsync(new PlaceQuery
            {
                Search = busca,
                Culture = currentCulture,
                PageSize = 12
            }, ct);
        }

        // Se for a capa principal (sem filtros), carregar dados agregados para os blocos editoriais da Home
        if (string.IsNullOrWhiteSpace(categoria) && string.IsNullOrWhiteSpace(busca) && string.IsNullOrWhiteSpace(bairro) && pagina == 1)
        {
            try
            {
                ViewData["UpcomingEvents"] = await content.SearchAsync(new ContentQuery
                {
                    OnlyUpcomingEvents = true,
                    Sort = ContentSort.Upcoming,
                    PageSize = 6,
                    Culture = currentCulture
                }, ct);

                ViewData["GastronomyPlaces"] = await places.SearchAsync(new PlaceQuery
                {
                    Category = "gastronomia",
                    PageSize = 4,
                    Culture = currentCulture
                }, ct);

                if (transitRepo is not null)
                {
                    ViewData["TransitAlerts"] = await transitRepo.GetActiveAlertsAsync(ct);
                    ViewData["TransitLines"] = await transitRepo.GetLinesAsync(null, ct);
                }

                if (neighborhoodRepo is not null)
                {
                    ViewData["Neighborhoods"] = await neighborhoodRepo.GetNeighborhoodsAsync(ct);
                }

                if (touristRepo is not null)
                {
                    ViewData["TouristItineraries"] = await touristRepo.GetItinerariesAsync(ct);
                }

                if (radarRepo is not null)
                {
                    ViewData["SocialVideos"] = await radarRepo.SocialPostsAsync(4, null, ct);
                }
            }
            catch
            {
                // Degradação graciosa em testes
            }
        }

        return View(await content.SearchAsync(new ContentQuery
        {
            Category = category,
            Search = busca,
            Subject = assunto,
            Neighborhood = bairro,
            Municipality = municipio,
            FreeOnly = gratuito,
            DateFrom = de,
            DateTo = ate,
            OnlyUpcomingEvents = categoria == "eventos",
            // A capa tem um bloco próprio de próximos eventos: a lista geral não os repete.
            ExcludeEvents = categoria != "eventos" && (string.IsNullOrWhiteSpace(busca) || !string.IsNullOrWhiteSpace(categoria)),
            Page = pagina,
            Culture = currentCulture
        }, ct));
    }

    public async Task<IActionResult> Detalhe(long id, CancellationToken ct)
    {
        var item = await content.GetAsync(id, ct);
        if (item is null || !item.IsVisibleToPublic(DateTime.UtcNow)) return NotFound();

        var currentCulture = CultureInfo.CurrentUICulture.Name;
        if (!currentCulture.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            var trans = await translationRepo.GetContentTranslationAsync(id, currentCulture, ct);
            if (trans is not null && trans.ReviewStatus == TranslationReviewStatus.Approved)
            {
                item.LocalizedTitle = trans.Title;
                item.LocalizedSummary = trans.Summary;
                item.LocalizedBody = trans.Body;
                item.CulturalNotes = trans.CulturalNotes;
            }
        }

        var history = new List<(DateTime Date, string Note)>();
        foreach (var revision in (await content.RevisionsAsync(id, ct)).Take(30))
        {
            using var snapshot = System.Text.Json.JsonDocument.Parse(revision.SnapshotJson);
            var root = snapshot.RootElement;
            if (!root.TryGetProperty("Status", out var status) || status.ToString() is not ("2" or "5" or "Published" or "Cancelled")) continue;
            var note = root.TryGetProperty("EventDetails", out var details) && details.ValueKind == System.Text.Json.JsonValueKind.Object && details.TryGetProperty("PublicUpdateNote", out var publicNote) && publicNote.ValueKind == System.Text.Json.JsonValueKind.String ? publicNote.GetString() : null;
            history.Add((revision.ChangedAt, string.IsNullOrWhiteSpace(note) ? status.ToString() is "5" or "Cancelled" ? "Cancelamento confirmado pela equipe." : "Publicação ou atualização após conferência editorial." : note));
        }
        ViewData["PublicHistory"] = history.Take(10).ToArray();
        return View(item);
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
    [HttpGet("robots.txt")] public IActionResult Robots() => Content("User-agent: *\nDisallow: /admin\nDisallow: /conta\nDisallow: /editorial\nSitemap: https://minhabelem.com.br/sitemap.xml\n", "text/plain");

    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        System.Xml.Linq.XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        System.Xml.Linq.XNamespace xhtml = "http://www.w3.org/1999/xhtml";

        var baseUri = "https://minhabelem.com.br";
        var staticPaths = new[] { "", "explore", "agenda", "guia", "videos", "midias", "guia-ia", "privacidade", "termos" };

        var urlElements = new List<System.Xml.Linq.XElement>();

        foreach (var path in staticPaths)
        {
            var loc = string.IsNullOrEmpty(path) ? baseUri + "/" : $"{baseUri}/{path}";
            var urlElem = new System.Xml.Linq.XElement(ns + "url",
                new System.Xml.Linq.XElement(ns + "loc", loc),
                new System.Xml.Linq.XElement(ns + "changefreq", "daily"),
                new System.Xml.Linq.XElement(ns + "priority", string.IsNullOrEmpty(path) ? "1.0" : "0.8"));

            foreach (var lang in SupportedCultures.All)
            {
                var langLoc = string.IsNullOrEmpty(path) ? $"{baseUri}/{lang}" : $"{baseUri}/{lang}/{path}";
                urlElem.Add(new System.Xml.Linq.XElement(xhtml + "link",
                    new System.Xml.Linq.XAttribute("rel", "alternate"),
                    new System.Xml.Linq.XAttribute("hreflang", lang),
                    new System.Xml.Linq.XAttribute("href", langLoc)));
            }
            urlElem.Add(new System.Xml.Linq.XElement(xhtml + "link",
                new System.Xml.Linq.XAttribute("rel", "alternate"),
                new System.Xml.Linq.XAttribute("hreflang", "x-default"),
                new System.Xml.Linq.XAttribute("href", loc)));

            urlElements.Add(urlElem);
        }

        var rows = await content.SearchAsync(new ContentQuery { PageSize = 1000 }, ct);
        foreach (var item in rows)
        {
            var itemLoc = $"{baseUri}/Home/Detalhe/{item.Id}";
            var urlElem = new System.Xml.Linq.XElement(ns + "url",
                new System.Xml.Linq.XElement(ns + "loc", itemLoc),
                new System.Xml.Linq.XElement(ns + "lastmod", item.UpdatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new System.Xml.Linq.XElement(ns + "changefreq", "weekly"),
                new System.Xml.Linq.XElement(ns + "priority", "0.7"));

            foreach (var lang in SupportedCultures.All)
            {
                urlElem.Add(new System.Xml.Linq.XElement(xhtml + "link",
                    new System.Xml.Linq.XAttribute("rel", "alternate"),
                    new System.Xml.Linq.XAttribute("hreflang", lang),
                    new System.Xml.Linq.XAttribute("href", $"{baseUri}/{lang}/Home/Detalhe/{item.Id}")));
            }
            urlElem.Add(new System.Xml.Linq.XElement(xhtml + "link",
                new System.Xml.Linq.XAttribute("rel", "alternate"),
                new System.Xml.Linq.XAttribute("hreflang", "x-default"),
                new System.Xml.Linq.XAttribute("href", itemLoc)));

            urlElements.Add(urlElem);
        }

        var root = new System.Xml.Linq.XElement(ns + "urlset",
            new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "xhtml", xhtml.NamespaceName),
            urlElements);

        return Content(new System.Xml.Linq.XDocument(root).ToString(), "application/xml; charset=utf-8");
    }
}

