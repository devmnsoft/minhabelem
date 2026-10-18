using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Core.Policies;
using MinhaBelem.Infrastructure.Identity;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class EditorialBatches(MySqlDataSource data, IContentRepository content, IIngestionRepository ingestion,
    IAdministrationRepository admin, IConfiguration config, IAiAnalysisService ai, IContentTranslationService translations, ITranslationRepository languages) : IEditorialBatches
{
    public async Task<IReadOnlyList<EditorialBatch>> RecentAsync(long actorId, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        return (await db.QueryAsync<EditorialBatch>(new CommandDefinition("""
            SELECT id,actor_id AS ActorId,action,reason,status,created_at AS CreatedAt
            FROM mb_editorial_batches WHERE actor_id=@actorId ORDER BY created_at DESC,id DESC LIMIT 30
            """, new { actorId }, cancellationToken: ct))).AsList();
    }
    public async Task<IReadOnlyDictionary<string,long>> DashboardAsync(CancellationToken ct)
    {
        await using var db=await data.OpenConnectionAsync(ct);
        var rows=await db.QueryAsync<(string Label,long Count)>(new CommandDefinition("""
            SELECT 'PendingStatus' AS Label,COUNT(*) AS Count FROM mb_ingestion_candidates WHERE status='Pending'
            UNION ALL SELECT 'BatchApprovedToday',COUNT(*) FROM mb_ingestion_candidates WHERE status='Approved' AND updated_at>=UTC_DATE()
            UNION ALL SELECT 'BatchRejected',COUNT(*) FROM mb_ingestion_candidates WHERE status='Rejected'
            UNION ALL SELECT 'BatchSensitive',COUNT(*) FROM mb_content_items WHERE flags<>0 OR category='Politica'
            UNION ALL SELECT 'BatchDeduplicate',COUNT(*) FROM mb_ingestion_candidates WHERE status='Duplicate'
            UNION ALL SELECT 'BatchFuturePending',COUNT(*) FROM mb_content_items WHERE kind='Event' AND event_start_at>UTC_TIMESTAMP() AND verification_status<>'Verified'
            UNION ALL SELECT 'BatchRunning',COUNT(*) FROM mb_editorial_batches WHERE status IN ('queued','running')
            UNION ALL SELECT 'BatchCompleted',COUNT(*) FROM mb_editorial_batches WHERE status='completed'
            UNION ALL SELECT 'BatchFailures',COUNT(*) FROM mb_editorial_batches WHERE status IN ('failed','partial')
            """,cancellationToken:ct));
        return rows.ToDictionary(x=>x.Label,x=>x.Count);
    }
    public static string Permission(string action) => action switch
    {
        "approve" or "rejectCandidate" or "reviewCandidate" or "retry" => Permissions.CandidateReview,
        "publish" or "archive" or "eventStatus" => Permissions.ContentPublish,
        "category" or "locality" or "review" or "reject" or "deduplicate" => Permissions.ContentWrite,
        "analyze" => Permissions.AiRun,
        "translate" => Permissions.TranslationManage,
        _ => throw new ArgumentException("Ação inválida.")
    };
    private static long Actor(ClaimsPrincipal actor, string action)
    {
        var permission = Permission(action);
        if (!actor.HasClaim("amr", "mfa") || !actor.HasClaim(PermissionClaimsPrincipalFactory.PermissionClaimType, permission)) throw new UnauthorizedAccessException();
        return long.Parse(actor.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
    }
    public async Task<string> PreviewAsync(EditorialBatchInput input, ClaimsPrincipal actor, CancellationToken ct)
    {
        var actorId = Actor(actor, input.Action);
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length is < 5 or > 500 || input.Targets.Length is 0 || input.Targets.Length > Math.Clamp(config.GetValue("Editorial:BatchLimit", 50), 1, 100)
            || input.Targets.Any(x => x.Id <= 0 || x.Kind is not ("content" or "candidate")) || input.Targets.DistinctBy(x => (x.Kind, x.Id)).Count() != input.Targets.Length)
            throw new ArgumentException("Confira os itens, o motivo e o limite do lote.");
        if (input.Action == "category" && !Enum.TryParse<ContentCategory>(input.Value, out var category) || input.Action == "category" && !Enum.IsDefined(Enum.Parse<ContentCategory>(input.Value!))) throw new ArgumentException("Categoria inválida.");
        if (input.Action == "eventStatus" && input.Value is not ("Cancelled" or "Postponed" or "UncertainDate" or "AwaitingOrganizer" or "Completed")) throw new ArgumentException("Situação inválida.");
        if (input.Municipality?.Length > 120 || input.Neighborhood?.Length > 120) throw new ArgumentException("Localidade inválida.");
        if (input.Action == "translate" && input.Value is not ("en" or "fr" or "es" or "ru" or "zh-Hans" or "ja")) throw new ArgumentException("Idioma inválido.");
        var id = Guid.NewGuid().ToString("N");
        var payload = JsonSerializer.Serialize(input);
        await using var db = await data.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_editorial_batches(id,actor_id,action,reason,request_hash) VALUES(@id,@actorId,@action,@reason,@hash)", new { id, actorId, action = input.Action, reason = input.Reason.Trim(), hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))) }, tx, cancellationToken: ct));
        foreach (var target in input.Targets)
        {
            var title = target.Kind == "content" ? (await content.GetAsync(target.Id, ct))?.Title : (await ingestion.CandidateAsync(target.Id, ct))?.Title;
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_editorial_batch_items(batch_id,target_id,target_kind,revision,payload,result) VALUES(@id,@targetId,@kind,@revision,@payload,@title)", new { id, targetId = target.Id, kind = target.Kind, revision = target.Revision, payload, title = title is null ? "Registro inexistente" : title[..Math.Min(300, title.Length)] }, tx, cancellationToken: ct));
        }
        await tx.CommitAsync(ct);
        return id;
    }
    public async Task<EditorialBatch?> GetAsync(string id, long actorId, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        var batch = await db.QuerySingleOrDefaultAsync<EditorialBatch>(new CommandDefinition("SELECT id,actor_id AS ActorId,action,reason,status,created_at AS CreatedAt FROM mb_editorial_batches WHERE id=@id AND actor_id=@actorId", new { id, actorId }, cancellationToken: ct));
        if (batch is not null) batch.Items = (await db.QueryAsync<EditorialBatchItem>(new CommandDefinition("SELECT target_id AS TargetId,target_kind AS TargetKind,revision,status,result,payload FROM mb_editorial_batch_items WHERE batch_id=@id ORDER BY target_kind,target_id", new { id }, cancellationToken: ct))).AsList();
        return batch;
    }
    public async Task<bool> ConfirmAsync(string id, ClaimsPrincipal actor, CancellationToken ct)
    {
        var actorId = long.Parse(actor.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var batch = await GetAsync(id, actorId, ct);
        if (batch is null) return false;
        Actor(actor, batch.Action);
        await using var db = await data.OpenConnectionAsync(ct);
        // Repeated confirmation returns the SAME batch, never creates a second operation.
        await db.ExecuteAsync(new CommandDefinition("UPDATE mb_editorial_batches SET status='queued' WHERE id=@id AND actor_id=@actorId AND status='preview' AND created_at>DATE_SUB(UTC_TIMESTAMP(),INTERVAL 30 MINUTE)", new { id, actorId }, cancellationToken: ct));
        return await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_editorial_batches WHERE id=@id AND status IN ('queued','running','completed','partial','failed')", new { id }, cancellationToken: ct)) == 1;
    }
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        if (await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT GET_LOCK('mb_editorial_batches',0)", cancellationToken: ct)) != 1) return false;
        try
        {
            var batch = await db.QueryFirstOrDefaultAsync<EditorialBatch>(new CommandDefinition("SELECT id,actor_id AS ActorId,action,reason,status FROM mb_editorial_batches WHERE status IN ('queued','running') ORDER BY created_at LIMIT 1", cancellationToken: ct));
            if (batch is null) return false;
            // An interrupted external request must be reviewed, not charged/replayed silently.
            await db.ExecuteAsync(new CommandDefinition("UPDATE mb_editorial_batch_items SET status='failed',result='Solicitação externa interrompida; confira o histórico antes de solicitar novamente.' WHERE batch_id=@Id AND status='running'", batch, cancellationToken: ct));
            await db.ExecuteAsync(new CommandDefinition("UPDATE mb_editorial_batches SET status='running' WHERE id=@Id", batch, cancellationToken: ct));
            var items = (await db.QueryAsync<EditorialBatchItem>(new CommandDefinition("SELECT target_id AS TargetId,target_kind AS TargetKind,revision,payload FROM mb_editorial_batch_items WHERE batch_id=@Id AND status='pending' ORDER BY target_kind,target_id", batch, cancellationToken: ct))).AsList();
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                var enabled = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_users WHERE id=@ActorId AND enabled=1 AND two_factor_enabled=1 AND must_change_password=0 AND (lockout_end IS NULL OR lockout_end<UTC_TIMESTAMP())", batch, cancellationToken: ct)) == 1;
                var allowed = enabled && (await admin.PermissionsAsync(batch.ActorId, ct)).Contains(Permission(batch.Action));
                if (!allowed) { await FinishAsync(db, null, batch, item, "blocked", "Permissão, MFA ou acesso revogado.", ct); continue; }
                try
                {
                    if (batch.Action is "analyze" or "translate")
                        await db.ExecuteAsync(new CommandDefinition("UPDATE mb_editorial_batch_items SET status='running' WHERE batch_id=@batchId AND target_id=@TargetId AND target_kind=@TargetKind", new { batchId = batch.Id, item.TargetId, item.TargetKind }, cancellationToken: ct));
                    await using var tx = await db.BeginTransactionAsync(ct);
                    var result = item.TargetKind == "candidate" ? await CandidateAsync(db, tx, batch, item, ct) : await ContentAsync(db, tx, batch, item, ct);
                    await FinishAsync(db, tx, batch, item, result is null ? "completed" : "blocked", result ?? "Alteração concluída.", ct);
                    await tx.CommitAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { await FinishAsync(db, null, batch, item, "failed", "Operação interrompida: " + ex.GetType().Name, ct); }
            }
            await db.ExecuteAsync(new CommandDefinition("UPDATE mb_editorial_batches SET status=IF(EXISTS(SELECT 1 FROM mb_editorial_batch_items WHERE batch_id=@Id AND status<>'completed'),'partial','completed'),finished_at=UTC_TIMESTAMP(6) WHERE id=@Id", batch, cancellationToken: ct));
            return true;
        }
        finally { await db.ExecuteAsync("SELECT RELEASE_LOCK('mb_editorial_batches')"); }
    }
    private static async Task FinishAsync(MySqlConnection db, MySqlTransaction? tx, EditorialBatch batch, EditorialBatchItem item, string status, string result, CancellationToken ct)
    {
        await db.ExecuteAsync(new CommandDefinition("UPDATE mb_editorial_batch_items SET status=@status,result=@result WHERE batch_id=@batchId AND target_id=@TargetId AND target_kind=@TargetKind; INSERT INTO mb_audit_logs(actor_id,action,entity_type,entity_id,details) VALUES(@actorId,'editorial.batch_item',@TargetKind,@TargetId,@details)", new { status, result, batchId = batch.Id, item.TargetId, item.TargetKind, actorId = batch.ActorId, details = JsonSerializer.Serialize(new { batch.Id, batch.Action, batch.Reason, status, result }) }, tx, cancellationToken: ct));
    }
    private async Task<string?> ContentAsync(MySqlConnection db, MySqlTransaction tx, EditorialBatch batch, EditorialBatchItem target, CancellationToken ct)
    {
        var revision = await db.ExecuteScalarAsync<int?>(new CommandDefinition("SELECT revision FROM mb_content_items WHERE id=@TargetId FOR UPDATE", target, tx, cancellationToken: ct));
        if (revision is null || revision != target.Revision) return "Conteúdo alterado por outro editor; refaça a prévia.";
        var item = await content.GetAsync(target.TargetId, ct);
        if (item is null || item.Revision != target.Revision) return "Registro alterado.";
        var input = JsonSerializer.Deserialize<EditorialBatchInput>(target.Payload)!;
        var oldStatus = item.Status;
        if (batch.Action == "publish")
        {
            // Batch publishing is deliberately stricter than individual human publication.
            if (item.Category is ContentCategory.Politica || item.Flags != ModerationFlag.None || item.DuplicateOfId is not null || item.Audiences.Contains(Audience.Infantil) || new[] { "saude", "saúde", "policia", "polícia", "menor", "denuncia", "denúncia" }.Any(x => (item.Subject ?? "").Contains(x, StringComparison.OrdinalIgnoreCase))) return "Revisão individual obrigatória para conteúdo sensível ou duplicado.";
            if (item.Status != ContentStatus.InReview || item.VerificationStatus != VerificationStatus.Verified || string.IsNullOrWhiteSpace(item.AuthorName) || string.IsNullOrWhiteSpace(item.SourceName) || !SafeUrl.Validate(item.SourceUrl).IsValid || item.IsExpired(DateTime.UtcNow) || !PublicationPolicy.Evaluate(item, DateTime.UtcNow, false).Allowed) return "Verificação editorial incompleta.";
            if (item.Kind == ContentKind.Event && (item.EventStartAt is null || string.IsNullOrWhiteSpace(item.Address))) return "Data ou local incertos.";
            if (!string.IsNullOrWhiteSpace(item.ImageUrl) && string.IsNullOrWhiteSpace(item.ImageLicense)) return "Autorização de mídia ausente.";
            if(item.SourceId is {} sourceId && await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_ingestion_sources WHERE id=@sourceId AND enabled=1 AND approved=1 AND TRIM(COALESCE(license_note,''))<>''",new{sourceId},tx,cancellationToken:ct))!=1)return "Fonte sem autorização vigente de reutilização.";
            if(item.EventDetails is {Uncertain:true} || item.EventDetails?.Status is {} eventState && (eventState.Contains("cancel",StringComparison.OrdinalIgnoreCase)||eventState.Contains("postpon",StringComparison.OrdinalIgnoreCase)))return "Evento exige revisão individual da situação na fonte.";
            if(System.Text.RegularExpressions.Regex.IsMatch(item.Title+" "+item.Summary,@"(?i)\b(den[uú]ncia|pol[ií]cia|homic[ií]dio|crian[çc]a\s+(v[ií]tima|desaparecida)|emerg[eê]ncia\s+de\s+sa[uú]de)\b",System.Text.RegularExpressions.RegexOptions.None,TimeSpan.FromMilliseconds(100)))return "Conteúdo sensível exige revisão individual.";
            item.Status = ContentStatus.Published; item.RequiresHumanApproval = false;
            var publishedBefore = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_content_revisions WHERE content_id=@Id AND JSON_UNQUOTE(JSON_EXTRACT(snapshot,'$.Status')) IN ('2','Published')", item, tx, cancellationToken: ct)) > 0;
            if (!publishedBefore) item.PublishedAt = DateTime.UtcNow;
        }
        else if (batch.Action == "archive") item.Status = ContentStatus.Archived;
        else if (batch.Action == "eventStatus") { if (item.Kind != ContentKind.Event) return "O registro não é um evento."; item.Status = Enum.Parse<ContentStatus>(input.Value!); }
        else if (batch.Action == "reject") item.Status = ContentStatus.Rejected;
        else if (batch.Action == "review") item.Status = ContentStatus.InReview;
        else if (batch.Action == "category") { item.Category = Enum.Parse<ContentCategory>(input.Value!); item.Status = ContentStatus.InReview; }
        else if (batch.Action == "locality") { item.Municipality = input.Municipality; item.Neighborhood = input.Neighborhood; item.Status = ContentStatus.InReview; }
        else if (batch.Action == "deduplicate")
        {
            var other = (await content.SearchForDuplicatesAsync(item.Title, item.Summary, 50, ct)).FirstOrDefault(x => x.Id != item.Id && MinhaBelem.Core.Text.Hashing.ContentFingerprint(x.Title, x.Summary) == MinhaBelem.Core.Text.Hashing.ContentFingerprint(item.Title, item.Summary));
            if (other is not null) { item.DuplicateOfId = other.Id; item.Flags |= ModerationFlag.PossibleDuplicate; item.Status = ContentStatus.InReview; }
        }
        else if (batch.Action == "analyze")
        {
            var analysis = await ai.AnalyzeAsync(new(item.Title, item.Summary, item.SourceUrl, Array.Empty<ContentItem>()) { ActorId = batch.ActorId, TargetId = item.Id }, ct);
            if (analysis.Runs.Count == 0) return "IA indisponível; mantenha revisão manual.";
            if (analysis.Classification is { } c) { item.Flags |= c.Flags; item.Category = c.Category; }
            if (analysis.IsDuplicate) { item.DuplicateOfId = analysis.DuplicateOfId; item.Flags |= ModerationFlag.PossibleDuplicate; }
            item.Status = ContentStatus.InReview;
        }
        else if (batch.Action == "translate")
        {
            if ((await languages.GetLanguageAsync(input.Value!, ct)) is not { IsEnabled: true, RequiresHumanApproval: true }) return "Tradução em lote exige idioma habilitado com revisão humana obrigatória.";
            var translated = await translations.TranslateContentAsync(item, input.Value!, batch.ActorId, ct);
            return translated.Success ? null : "Tradução indisponível; confira o histórico de IA.";
        }
        else return "Ação incompatível com conteúdo.";
        if (item.Status == ContentStatus.InReview) { item.RequiresHumanApproval = true; item.VerificationStatus = VerificationStatus.Pending; }
        item.Revision++; item.ReviewedById = batch.ActorId; item.UpdatedAt = DateTime.UtcNow;
        await db.ExecuteAsync(new CommandDefinition("UPDATE mb_content_items SET category=@category,municipality=@Municipality,neighborhood=@Neighborhood,status=@status,verification_status=@verification,flags=@flags,duplicate_of_id=@DuplicateOfId,requires_human_approval=@RequiresHumanApproval,published_at=@PublishedAt,revision=@Revision,reviewed_by_id=@actorId,updated_at=UTC_TIMESTAMP(6) WHERE id=@Id AND revision=@oldRevision; INSERT INTO mb_content_revisions(content_id,revision,change_summary,snapshot,changed_by_id) VALUES(@Id,@Revision,@reason,@snapshot,@actorId)", new { item.Id, item.Revision, oldRevision = target.Revision, category = item.Category.ToString(), item.Municipality, item.Neighborhood, status = item.Status.ToString(), verification=item.VerificationStatus.ToString(), flags = (int)item.Flags, item.DuplicateOfId, item.RequiresHumanApproval, item.PublishedAt, actorId = batch.ActorId, reason = batch.Reason, snapshot = JsonSerializer.Serialize(item) }, tx, cancellationToken: ct));
        return null;
    }
    private async Task<string?> CandidateAsync(MySqlConnection db, MySqlTransaction tx, EditorialBatch batch, EditorialBatchItem target, CancellationToken ct)
    {
        var row = await db.QuerySingleOrDefaultAsync<IngestionCandidate>(new CommandDefinition("SELECT id,updated_at AS UpdatedAt,status,content_id AS ContentId,title,summary,source_id AS SourceId,source_name AS SourceName,source_url AS SourceUrl,source_url_hash AS SourceUrlHash,author_name AS AuthorName,published_at AS PublishedAt FROM mb_ingestion_candidates WHERE id=@TargetId FOR UPDATE", target, tx, cancellationToken: ct));
        if (row is null || row.UpdatedAt.Ticks != target.Revision) return "Candidato alterado; refaça a prévia.";
        if (row.ContentId is not null) return "Candidato já importado; revise o conteúdo individualmente.";
        if (batch.Action == "approve")
        {
            if (row.Status is not (CandidateStatus.Pending or CandidateStatus.InReview)) return "Estado incompatível com importação.";
            var slug = MinhaBelem.Core.Text.HtmlText.Slugify(row.Title);
            var id = await db.ExecuteScalarAsync<long>(new CommandDefinition("INSERT INTO mb_content_items(kind,category,title,slug,summary,source_id,source_name,source_url,source_url_hash,fingerprint,author_name,source_published_at,status,created_by_id) VALUES('News','Cidades',@Title,@slug,@Summary,@SourceId,@SourceName,@SourceUrl,@SourceUrlHash,@fingerprint,@AuthorName,@PublishedAt,'InReview',@actorId); SELECT LAST_INSERT_ID()", new { row.Title, row.Summary, row.SourceId, row.SourceName, row.SourceUrl, row.SourceUrlHash, row.AuthorName, row.PublishedAt, actorId = batch.ActorId, slug = slug[..Math.Min(120, slug.Length)] + "-" + row.Id, fingerprint = MinhaBelem.Core.Text.Hashing.ContentFingerprint(row.Title, row.Summary) }, tx, cancellationToken: ct));
            var version = await db.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT snapshot FROM mb_discovery_versions WHERE candidate_id=@Id ORDER BY id DESC LIMIT 1", row, tx, cancellationToken: ct));
            var evidence = version is null ? null : JsonSerializer.Deserialize<MinhaBelem.Core.Services.DiscoveredItem>(version)?.Event;
            if (evidence is not null)
                await db.ExecuteAsync(new CommandDefinition("UPDATE mb_content_items SET kind='Event',event_start_at=@StartUtc,event_end_at=@EndUtc,address=@Location,is_free=@IsFree,ticket_price_text=@Price WHERE id=@contentId; INSERT INTO mb_event_evidence(content_id,evidence) VALUES(@contentId,@evidence)", new { contentId = id, evidence.StartUtc, evidence.EndUtc, evidence.Location, evidence.IsFree, evidence.Price, evidence = JsonSerializer.Serialize(evidence) }, tx, cancellationToken: ct));
            await db.ExecuteAsync(new CommandDefinition("UPDATE mb_ingestion_candidates SET content_id=@contentId WHERE id=@candidateId; INSERT INTO mb_content_revisions(content_id,revision,change_summary,snapshot,changed_by_id) VALUES(@contentId,1,@reason,@snapshot,@actorId)", new { contentId = id, candidateId = row.Id, reason = batch.Reason, snapshot = JsonSerializer.Serialize(row), actorId = batch.ActorId }, tx, cancellationToken: ct));
            row.Status = CandidateStatus.Approved;
        }
        else if (batch.Action == "rejectCandidate") row.Status = CandidateStatus.Rejected;
        else if (batch.Action is "reviewCandidate" or "retry") row.Status = CandidateStatus.InReview;
        else return "Ação incompatível com candidato.";
        await db.ExecuteAsync(new CommandDefinition("UPDATE mb_ingestion_candidates SET status=@status,review_notes=@Reason,updated_at=UTC_TIMESTAMP(6) WHERE id=@Id", new { status = row.Status.ToString(), batch.Reason, row.Id }, tx, cancellationToken: ct));
        return null;
    }
}

