using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Security;
using MinhaBelem.Web.Resources;

namespace MinhaBelem.Web.Controllers;

public sealed class CitySurfaceController(IPlacePhotoScout photos, IIngestionRepository ingestion, IContentRepository content) : Controller
{
    [HttpGet("/guia/acervo")]
    [HttpGet("/{culture:regex(^(pt-BR|en|fr|es|ru|zh-Hans|ja)$)}/guia/acervo")]
    public async Task<IActionResult> Acervo(CancellationToken ct)
    {
        ViewData["Title"] = "Acervo fotográfico";
        return View("~/Views/Guia/Acervo.cshtml", await photos.ApprovedAsync(null, null, ct));
    }

    [HttpGet("/salvos")]
    public IActionResult Salvos()
    {
        ViewData["Title"] = "Meus planos";
        return View("~/Views/Home/Salvos.cshtml");
    }

    [HttpGet("/operacao")]
    public async Task<IActionResult> Operacao(CancellationToken ct)
    {
        ViewData["Title"] = "Situação da plataforma";
        var desk = await photos.DeskAsync(ct);
        ViewData["Sources"] = (await ingestion.SourcesAsync(true, ct)).Count;
        ViewData["Upcoming"] = await content.CountAsync(new ContentQuery { OnlyUpcomingEvents = true }, ct);
        return View("~/Views/Home/Operacao.cshtml", desk);
    }

    [HttpGet("/widget/agenda")]
    public IActionResult Widget() => View("~/Views/Home/WidgetAgenda.cshtml");
}

[Route("admin/fotos"), Authorize(Policy = Permissions.PlaceManage)]
public sealed class AdminPhotosController(IPlacePhotoScout photos) : Controller
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Fotos dos locais";
        return View("~/Views/Admin/Fotos.cshtml", await photos.DeskAsync(ct));
    }

    [HttpPost("buscar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Buscar(CancellationToken ct)
    {
        var found = await photos.ScoutNextAsync(4, ct);
        TempData["PhotoMessage"] = found + " fotos de licença aberta gravadas neste pedido. A busca respeita o intervalo de sete dias por local.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:long}/revisar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Revisar(long id, bool aprovar, CancellationToken ct)
    {
        await photos.ReviewAsync(id, aprovar, Actor, ct);
        return RedirectToAction(nameof(Index));
    }
}

[Route("admin/visitas"), Authorize(Policy = Permissions.PlaceManage)]
public sealed class AdminVisitsController(IVisitRequests requests) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Pedidos de visita";
        return View("~/Views/Admin/Visitas.cshtml", await requests.PendingAsync(ct));
    }

    [HttpPost("{id:long}/tratar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Tratar(long id, CancellationToken ct)
    {
        await requests.HandleAsync(id, ct);
        TempData["VisitMessage"] = LocalizationCatalog.Get("VisitRequestHandled");
        return RedirectToAction(nameof(Index));
    }
}
