using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class EditorialWorkflow(MySqlDataSource data) : IEditorialWorkflow
{
    public async Task<long?> ImportAsync(long candidateId, long? actorId, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var row = await db.QuerySingleOrDefaultAsync<IngestionCandidate>(new CommandDefinition("SELECT id AS Id, content_id AS ContentId, title AS Title, summary AS Summary, source_name AS SourceName, source_url AS SourceUrl, source_url_hash AS SourceUrlHash, source_id AS SourceId, published_at AS PublishedAt, author_name AS AuthorName, status AS Status FROM mb_ingestion_candidates WHERE id=@candidateId FOR UPDATE", new { candidateId }, tx, cancellationToken: ct));
        if (row is null) return null;
        if (row.ContentId is { } existing) return existing;
        if (row.Status is CandidateStatus.Rejected or CandidateStatus.Duplicate) return null;
        var id = await db.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO mb_content_items (kind,category,title,slug,summary,source_id,source_name,source_url,source_url_hash,fingerprint,author_name,source_published_at,status,created_by_id)
            VALUES ('News','Cidades',@Title,@slug,@Summary,@SourceId,@SourceName,@SourceUrl,@hash,@fingerprint,@AuthorName,@PublishedAt,'InReview',@actorId)
            ON DUPLICATE KEY UPDATE id=LAST_INSERT_ID(id);
            SELECT LAST_INSERT_ID();
            """, new { row.Title, row.Summary, row.SourceId, row.SourceName, row.SourceUrl, row.AuthorName, row.PublishedAt, actorId, slug = HtmlText.Slugify(row.Title)[..Math.Min(120, HtmlText.Slugify(row.Title).Length)] + "-" + candidateId, hash = row.SourceUrlHash, fingerprint = Hashing.ContentFingerprint(row.Title, row.Summary) }, tx, cancellationToken: ct));
        await db.ExecuteAsync(new CommandDefinition("UPDATE mb_ingestion_candidates SET content_id=@id,status='Approved',review_notes='Importado para revisão humana' WHERE id=@candidateId", new { id, candidateId }, tx, cancellationToken: ct));
        await db.ExecuteAsync(new CommandDefinition("INSERT IGNORE INTO mb_content_revisions (content_id,revision,change_summary,snapshot,changed_by_id) VALUES (@id,1,'Importação de candidato',@snapshot,@actorId)", new { id, actorId, snapshot = System.Text.Json.JsonSerializer.Serialize(row) }, tx, cancellationToken: ct));
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_audit_logs (actor_id,action,entity_type,entity_id,details) VALUES (@actorId,'candidate.imported','candidate',@candidateId,JSON_OBJECT('contentId',@id))", new { actorId, candidateId, id }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return id;
    }
}
