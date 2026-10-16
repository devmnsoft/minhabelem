using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Text;
using MinhaBelem.Web.Models;
using ContentItem = MinhaBelem.Core.Entities.ContentItem;
namespace MinhaBelem.Web.Controllers;

[Route("editorial"), Authorize(Policy = Permissions.ContentRead)]
public sealed class EditorialController(IContentRepository content, IIngestionRepository ingestion, IEditorialWorkflow workflow, IAiAnalysisService ai) : Controller
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(new EditorialIndex(
        await ingestion.CandidatesAsync(new CandidateQuery { Status = CandidateStatus.Pending }, ct),
        await content.EditorialAsync(new ContentQuery { PageSize = 100 }, ct)));
    [HttpPost("importar/{id:long}"), Authorize(Policy = Permissions.CandidateReview)]
    public async Task<IActionResult> Importar(long id, CancellationToken ct)
    {
        var imported = await workflow.ImportAsync(id, Actor, ct);
        return imported is null ? NotFound() : RedirectToAction(nameof(Editar), new { id = imported });
    }
    [HttpPost("rejeitar/{id:long}"), Authorize(Policy = Permissions.CandidateReview)]
    public async Task<IActionResult> Rejeitar(long id, CancellationToken ct)
    {
        var candidate = await ingestion.CandidateAsync(id, ct);
        if (candidate is null || candidate.ContentId is not null) return NotFound();
        await ingestion.SetCandidateStatusAsync(id, CandidateStatus.Rejected, Actor, "Rejeitado na central editorial", ct);
        return RedirectToAction(nameof(Index));
    }
    [HttpGet("editar/{id:long}"), Authorize(Policy = Permissions.ContentWrite)]
    public async Task<IActionResult> Editar(long id, CancellationToken ct)
    {
        var item = await content.GetAsync(id, ct);
        if (item is null) return NotFound();
        ViewData["Revisions"] = await content.RevisionsAsync(id, ct);
        ViewData["Status"] = item.Status;
        ViewData["Flags"] = item.Flags;
        return View(new ContentInput { Id = id, Revision = item.Revision, Title = item.Title, Summary = item.Summary, Body = item.Body, SourceName = item.SourceName, SourceUrl = item.SourceUrl, AuthorName = item.AuthorName ?? "", Category = item.Category, Kind = item.Kind, Municipality = item.Municipality, Neighborhood = item.Neighborhood, Address = item.Address, EventStartAt = item.EventStartAt, EventEndAt = item.EventEndAt, ExpiresAt = item.ExpiresAt, TicketPrice = item.TicketPrice, IsFree = item.IsFree, Verified = item.VerificationStatus == VerificationStatus.Verified });
    }
    [HttpPost("editar/{id:long}"), Authorize(Policy = Permissions.ContentWrite)]
    public async Task<IActionResult> Editar(long id, ContentInput model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();
        if (!SafeUrl.Validate(model.SourceUrl).IsValid) ModelState.AddModelError(nameof(model.SourceUrl), "Informe uma fonte HTTPS pública.");
        if (model.Kind == ContentKind.Event && (!model.EventStartAt.HasValue || model.EventEndAt < model.EventStartAt)) ModelState.AddModelError(nameof(model.EventStartAt), "Confira o início e término do evento, em UTC.");
        if (!ModelState.IsValid) { ViewData["Revisions"] = await content.RevisionsAsync(id, ct); return View(model); }
        var item = await content.GetAsync(id, ct);
        if (item is null) return NotFound();
        if (item.Revision != model.Revision) return Conflict("Outro editor alterou este conteúdo. Recarregue a página.");
        item.Title = HtmlText.Clean(model.Title, 300); item.Summary = HtmlText.ToPlainText(model.Summary, 2500);
        item.Body = HtmlText.ToPlainText(model.Body, 20000); item.SourceName = HtmlText.Clean(model.SourceName, 240); item.SourceUrl = model.SourceUrl.Trim();
        item.AuthorName = HtmlText.Clean(model.AuthorName, 240); item.Category = model.Category; item.Kind = model.Kind;
        item.Neighborhood = HtmlText.Clean(model.Neighborhood, 120); item.Municipality = HtmlText.Clean(model.Municipality, 120); item.Address = HtmlText.Clean(model.Address, 500);
        item.EventStartAt = model.EventStartAt; item.EventEndAt = model.EventEndAt; item.ExpiresAt = model.ExpiresAt;
        item.TicketPrice = model.TicketPrice; item.IsFree = model.IsFree;
        item.VerificationStatus = model.Verified ? VerificationStatus.Verified : VerificationStatus.Pending;
        if (model.Verified) item.LastVerifiedAt = DateTime.UtcNow;
        // Toda alteracao publicada retorna a revisao; apenas a acao Publicar pode expor novamente.
        item.Status = ContentStatus.InReview; item.RequiresHumanApproval = true;
        return await content.UpdateAsync(item, Actor, model.ChangeSummary, ct) ? RedirectToAction(nameof(Editar), new { id }) : Conflict("Registro alterado por outro editor.");
    }
    [HttpPost("publicar/{id:long}"), Authorize(Policy = Permissions.ContentPublish)]
    public async Task<IActionResult> Publicar(long id, CancellationToken ct)
    {
        if (!await content.SetStatusAsync(id, ContentStatus.Published, Actor, ct))
        { TempData["Notice"] = "Publicação bloqueada. Confirme autoria, fonte, verificação, datas e o estado de revisão."; return RedirectToAction(nameof(Editar), new { id }); }
        TempData["Notice"] = "Conteúdo publicado com responsabilidade editorial registrada.";
        return RedirectToAction(nameof(Editar), new { id });
    }
    [HttpPost("arquivar/{id:long}"), Authorize(Policy = Permissions.ContentPublish)]
    public async Task<IActionResult> Arquivar(long id, CancellationToken ct)
    {
        await content.SetStatusAsync(id, ContentStatus.Archived, Actor, ct);
        return RedirectToAction(nameof(Index));
    }
    [HttpPost("analisar/{id:long}"), Authorize(Policy = Permissions.AiRun)]
    public async Task<IActionResult> Analisar(long id, CancellationToken ct)
    {
        var item = await content.GetAsync(id, ct);
        if (item is null) return NotFound();
        var comparisons = (await content.SearchForDuplicatesAsync(item.Title, item.Summary, 10, ct)).Where(x => x.Id != id).ToArray();
        var analysis = await ai.AnalyzeAsync(new(item.Title, item.Summary, item.SourceUrl, comparisons) { TargetId = id, ActorId = Actor }, ct);
        if (analysis.Classification is { } c) { item.Category = c.Category; item.Subject = c.Subject; item.Flags |= c.Flags; item.Neighborhood = c.Neighborhood; item.Municipality = c.Municipality; item.Audiences = c.Audiences; }
        item.Status = ContentStatus.InReview; item.RequiresHumanApproval = true;
        if (analysis.IsDuplicate) item.Flags |= ModerationFlag.PossibleDuplicate;
        await content.UpdateAsync(item, Actor, "Análise de IA para revisão humana", ct);
        TempData["Notice"] = analysis.Runs.Count == 0 ? "IA não configurada. A revisão manual está disponível." : "Análise registrada. Confira as sugestões e as fontes antes de publicar.";
        return RedirectToAction(nameof(Editar), new { id });
    }
}
