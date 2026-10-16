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
public sealed class EditorialController(IContentRepository content, IIngestionRepository ingestion, IEditorialWorkflow workflow, IAiAnalysisService ai, IDiscoveryRepository discovery, IMediaAnalysis media) : Controller
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpPost("ocr/{id:long}"), Authorize(Policy = Permissions.AiRun)]
    public async Task<IActionResult> Ocr(long id, CancellationToken ct)
    {
        var candidate = await ingestion.CandidateAsync(id, ct);
        if (candidate is null) return NotFound();
        try
        {
            var mediaResult = await media.AnalyzeAsync(id, Actor, ct);
            var result = await ai.AnalyzeAsync(new(candidate.Title, candidate.Summary + "\nTexto OCR não verificado: " + mediaResult.Text, candidate.SourceUrl, Array.Empty<ContentItem>()) { ActorId = Actor, TargetId = candidate.ContentId }, ct);
            ViewData["Analysis"] = result;
            return View("Ocr", mediaResult);
        }
        catch (InvalidOperationException ex) { TempData["Notice"] = ex.Message; return RedirectToAction(nameof(Comparar), new { id }); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { TempData["Notice"] = "A análise atingiu o limite de tempo. Nenhuma publicação foi feita."; return RedirectToAction(nameof(Comparar), new { id }); }
        catch (HttpRequestException) { TempData["Notice"] = "Não foi possível obter a mídia autorizada. Confira a disponibilidade na fonte."; return RedirectToAction(nameof(Comparar), new { id }); }
    }
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
        return View(new ContentInput { Id = id, Revision = item.Revision, Title = item.Title, Summary = item.Summary, Body = item.Body, SourceName = item.SourceName, SourceUrl = item.SourceUrl, AuthorName = item.AuthorName ?? "", Category = item.Category, Kind = item.Kind, Municipality = item.Municipality, Neighborhood = item.Neighborhood, Address = item.Address, EventStartAt = item.EventStartAt, EventEndAt = item.EventEndAt, ExpiresAt = item.ExpiresAt, TicketPrice = item.TicketPrice, IsFree = item.IsFree, Verified = item.VerificationStatus == VerificationStatus.Verified, Subject = item.Subject, Audiences = item.Audiences.ToArray(), Accessibility = item.Accessibility.ToArray(), Organizer = item.EventDetails?.Organizer, Participants = item.EventDetails?.Participants, PublicUpdateNote = item.EventDetails?.PublicUpdateNote, MinimumAge = int.TryParse(item.EventDetails?.AgeRating, out var age) ? age : null });
    }
    [HttpPost("editar/{id:long}"), Authorize(Policy = Permissions.ContentWrite)]
    public async Task<IActionResult> Editar(long id, ContentInput model, CancellationToken ct)
    {
        if (id != model.Id) return BadRequest();
        if (model.Audiences.Length > 4 || model.Audiences.Any(x => !Enum.IsDefined(x)) || model.Accessibility.Length > 8 || model.Accessibility.Any(x => !Enum.IsDefined(x))) ModelState.AddModelError("", "Selecione públicos e acessibilidade válidos.");
        if (!SafeUrl.Validate(model.SourceUrl).IsValid) ModelState.AddModelError(nameof(model.SourceUrl), "Informe uma fonte HTTPS pública.");
        if (model.Kind == ContentKind.Event && (!model.EventStartAt.HasValue || model.EventEndAt < model.EventStartAt)) ModelState.AddModelError(nameof(model.EventStartAt), "Confira o início e término do evento, em UTC.");
        if (!ModelState.IsValid) { ViewData["Revisions"] = await content.RevisionsAsync(id, ct); return View(model); }
        var item = await content.GetAsync(id, ct);
        if (item is null) return NotFound();
        if (item.Revision != model.Revision) return Conflict("Outro editor alterou este conteúdo. Recarregue a página.");
        item.Title = HtmlText.Clean(model.Title, 300); item.Summary = HtmlText.ToPlainText(model.Summary, 2500);
        item.Body = HtmlText.ToPlainText(model.Body, 20000); item.SourceName = HtmlText.Clean(model.SourceName, 240);
        if (!string.Equals(item.SourceUrl, model.SourceUrl.Trim(), StringComparison.Ordinal)) item.SourceUrlHash = item.Kind == ContentKind.Event ? Hashing.Sha256(Hashing.NormalizeUrl(model.SourceUrl) + "|content:" + item.Id) : Hashing.UrlHash(model.SourceUrl);
        item.SourceUrl = model.SourceUrl.Trim();
        item.AuthorName = HtmlText.Clean(model.AuthorName, 240); item.Category = model.Category; item.Kind = model.Kind;
        item.Neighborhood = HtmlText.Clean(model.Neighborhood, 120); item.Municipality = HtmlText.Clean(model.Municipality, 120); item.Address = HtmlText.Clean(model.Address, 500);
        item.EventStartAt = model.EventStartAt; item.EventEndAt = model.EventEndAt; item.ExpiresAt = model.ExpiresAt;
        item.TicketPrice = model.TicketPrice; item.IsFree = model.IsFree;
        if (model.ResolveDuplicate) { item.DuplicateOfId = null; item.Flags &= ~ModerationFlag.PossibleDuplicate; }
        item.Subject = model.Subject; item.Audiences = model.Audiences; item.Accessibility = model.Accessibility;
        if (model.Kind == ContentKind.Event)
            item.EventDetails = new(model.EventStartAt, model.EventEndAt, item.Address, HtmlText.Clean(model.Organizer, 240), item.EventDetails?.Status, model.TicketPrice?.ToString(System.Globalization.CultureInfo.InvariantCulture), model.IsFree, model.MinimumAge?.ToString(), HtmlText.Clean(model.Participants, 500), item.SourceUrl, model.EventStartAt is null) { PublicUpdateNote = HtmlText.Clean(model.PublicUpdateNote, 300), Availability = item.EventDetails?.Availability };
        item.VerificationStatus = model.Verified ? VerificationStatus.Verified : VerificationStatus.Pending;
        if (model.Verified) item.LastVerifiedAt = DateTime.UtcNow;
        // Toda alteracao publicada retorna a revisao; apenas a acao Publicar pode expor novamente.
        item.Status = ContentStatus.InReview; item.RequiresHumanApproval = true;
        return await content.UpdateAsync(item, Actor, model.ChangeSummary, ct) ? RedirectToAction(nameof(Editar), new { id }) : Conflict("Registro alterado por outro editor.");
    }
    [HttpPost("publicar/{id:long}"), Authorize(Policy = Permissions.ContentPublish)]
    public async Task<IActionResult> Publicar(long id, bool confirmSensitive, CancellationToken ct)
    {
        var item = await content.GetAsync(id, ct);
        if (item is null) return NotFound();
        if (!confirmSensitive && (item.Category == Core.Taxonomy.ContentCategory.Politica || (item.Flags & (ModerationFlag.Sensitive | ModerationFlag.CriticalAlert | ModerationFlag.Denunciation | ModerationFlag.Offensive | ModerationFlag.PossibleDuplicate)) != 0))
        { TempData["Notice"] = "Conteúdo sensível exige confirmação explícita da revisão humana das evidências."; return RedirectToAction(nameof(Editar), new { id }); }
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
    [HttpPost("situacao/{id:long}"), Authorize(Policy = Permissions.ContentPublish)]
    public async Task<IActionResult> Situacao(long id, ContentStatus status, CancellationToken ct)
    {
        if (status is not (ContentStatus.Cancelled or ContentStatus.Postponed or ContentStatus.UncertainDate or ContentStatus.AwaitingOrganizer or ContentStatus.Completed)) return BadRequest();
        await content.SetStatusAsync(id, status, Actor, ct);
        return RedirectToAction(nameof(Editar), new { id });
    }
    [HttpGet("comparar/{id:long}"), Authorize(Policy = Permissions.CandidateRead)]
    public async Task<IActionResult> Comparar(long id, CancellationToken ct)
    {
        var candidate = await ingestion.CandidateAsync(id, ct);
        if (candidate is null) return NotFound();
        return View(new ComparisonPage(candidate, candidate.ContentId is { } contentId ? await content.GetAsync(contentId, ct) : null, await discovery.EvidenceAsync(id, ct)));
    }
    [HttpPost("atualizacao/{id:long}"), Authorize(Policy = Permissions.ContentWrite)]
    public async Task<IActionResult> AplicarAtualizacao(long id, int revision, long candidateTicks, CancellationToken ct)
    {
        var candidate = await ingestion.CandidateAsync(id, ct);
        if (candidate?.ContentId is not { } contentId || candidate.Status != CandidateStatus.Pending) return BadRequest();
        var item = await content.GetAsync(contentId, ct);
        if (item is null) return NotFound();
        if (item.Revision != revision || candidate.UpdatedAt.Ticks != candidateTicks) return Conflict("Conteúdo ou fonte mudou. Recarregue a comparação.");
        item.Title = candidate.Title; item.Summary = candidate.Summary;
        item.EventDetails = await discovery.EvidenceAsync(id, ct);
        if (item.EventDetails is { } ev) { item.EventStartAt = ev.StartUtc; item.EventEndAt = ev.EndUtc; item.Address = ev.Location; item.Kind = ContentKind.Event; }
        item.Status = ContentStatus.InReview; item.RequiresHumanApproval = true; item.VerificationStatus = VerificationStatus.Pending;
        if (!await content.UpdateAsync(item, Actor, "Atualização da fonte importada para conferência humana", ct)) return Conflict();
        await ingestion.SetCandidateStatusAsync(id, CandidateStatus.Approved, Actor, "Atualização encaminhada para edição", ct);
        return RedirectToAction(nameof(Editar), new { id = contentId });
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
        item.VerificationStatus = VerificationStatus.Pending;
        if (analysis.IsDuplicate) { item.Flags |= ModerationFlag.PossibleDuplicate; item.DuplicateOfId = analysis.DuplicateOfId; }
        await content.UpdateAsync(item, Actor, "Análise de IA para revisão humana", ct);
        TempData["Notice"] = analysis.Runs.Count == 0 ? "Análise automática indisponível ou retida por segurança. Siga pela revisão manual." : "Análise registrada. Confira as sugestões e as fontes antes de publicar.";
        return RedirectToAction(nameof(Editar), new { id });
    }
}
