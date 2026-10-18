using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Security;
namespace MinhaBelem.Web.Controllers;

[Route("editorial/lotes"), Authorize(Policy = Permissions.ContentRead)]
public sealed class EditorialBatchesController(IEditorialBatches batches, IContentRepository content, IIngestionRepository ingestion,IConfiguration config) : Controller
{
    private int Limit => Math.Clamp(config.GetValue("Editorial:BatchLimit",250),1,EditorialBatchLimits.Maximum);
    [HttpGet("")]
    public async Task<IActionResult> Index(string kind = "content", string? search = null, int page = 1, CancellationToken ct = default)
    {
        if(kind is not("content" or "candidate") || search?.Length>120 || page is <1 or >10000)return BadRequest();
        ViewData["Kind"] = kind; ViewData["Search"] = search; ViewData["Page"] = Math.Max(1, page);
        ViewData["BatchLimit"]=Limit;
        ViewData["FilteredCount"]=kind=="candidate"?await ingestion.CountCandidatesAsync(new(){Search=search},ct):await content.CountEditorialAsync(new(){Search=search},ct);
        ViewData["BatchHistory"]=await batches.HistoryAsync(long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),ct);
        ViewData["BatchMetrics"] = await batches.DashboardAsync(ct);
        return View(kind == "candidate" ? (object)await ingestion.CandidatesAsync(new CandidateQuery { Search = search, Page = Math.Max(1, page), PageSize = 20 }, ct)
            : await content.EditorialAsync(new ContentQuery { Search = search, Page = Math.Max(1, page), PageSize = 20 }, ct));
    }
    [HttpPost("previa"),RequestSizeLimit(256000),RequestFormLimits(ValueCountLimit=4096)]
    public async Task<IActionResult> Preview(EditorialBatchInput input, string[] selected, string kind, string? search, bool allFiltered, int? confirmedCount, CancellationToken ct)
    {
        try
        {
            if(!ModelState.IsValid || kind is not("content" or "candidate") || search?.Length>120)return BadRequest();
            if (allFiltered)
            {
                var targets = kind == "candidate"
                    ? (await ingestion.CandidatesAsync(new CandidateQuery { Search = search, PageSize = Limit+1 }, ct)).Select(x => new BatchTarget(x.Id, "candidate", x.UpdatedAt.Ticks)).ToArray()
                    : (await content.EditorialAsync(new ContentQuery { Search = search, PageSize = Limit+1 }, ct)).Select(x => new BatchTarget(x.Id, "content", x.Revision)).ToArray();
                if (targets.Length>Limit || confirmedCount != targets.Length) return BadRequest($"Os resultados mudaram ou excedem {Limit} itens. Atualize a seleção e refine os filtros.");
                input.Targets = targets;
            }
            else input.Targets = selected.Select(x => x.Split(':')).Select(x => new BatchTarget(long.Parse(x[1]), x[0], long.Parse(x[2]))).ToArray();
            var id = await batches.PreviewAsync(input, User, ct);
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) when (ex is ArgumentException or FormatException or IndexOutOfRangeException or OverflowException) { return BadRequest("Confira a seleção, ação, motivo e limite do lote."); }
    }
    [HttpGet("{id}/progresso")]
    public async Task<IActionResult> Progress(string id,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        var batch=await batches.GetAsync(id,long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),ct);
        return batch is null?NotFound():Json(new{batch.Status,total=batch.Items.Count,finished=batch.Items.Count(x=>x.Status is "completed" or "blocked" or "failed"),results=batch.Items.Select(x=>new{x.TargetId,x.TargetKind,x.Status,x.Result})});
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> Details(string id, CancellationToken ct)
    {
        var batch = await batches.GetAsync(id, long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
        return batch is null ? NotFound() : View(batch);
    }
    [HttpPost("{id}/confirmar")]
    public async Task<IActionResult> Confirm(string id, bool confirmed, CancellationToken ct)
    {
        if (!confirmed) return BadRequest("Confirmação obrigatória.");
        try { return await batches.ConfirmAsync(id, User, ct) ? RedirectToAction(nameof(Details), new { id }) : Conflict(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
