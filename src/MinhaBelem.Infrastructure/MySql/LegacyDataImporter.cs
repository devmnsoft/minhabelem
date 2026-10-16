using Dapper;

namespace MinhaBelem.Infrastructure.MySql;

/// <summary>Importacao explicita e aditiva. Tabelas legadas nunca sao apagadas ou alteradas.</summary>
public sealed class LegacyDataImporter(MySqlDataSource data)
{
    public async Task ImportAsync(CancellationToken ct = default)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var collisions = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM content_items old JOIN mb_content_items current ON current.id=old.id WHERE current.source_url_hash<>old.source_url_hash", transaction: tx, cancellationToken: ct));
        if (collisions != 0) throw new InvalidOperationException("Há IDs conflitantes entre os conteúdos antigos e novos. Importação cancelada; planeje o mapeamento antes de continuar.");
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO mb_ingestion_sources (name,feed_url,feed_url_hash,protocol,enabled,approved,respect_robots,license_note)
            SELECT source_name,feed_url,feed_url_hash,CASE WHEN protocol='atom' THEN 'Atom' ELSE 'Rss' END,0,0,1,'Importada: autorização requer nova conferência'
            FROM ingestion_sources old WHERE NOT EXISTS (SELECT 1 FROM mb_ingestion_sources current WHERE current.feed_url_hash=old.feed_url_hash);
            INSERT INTO mb_content_items (id,kind,category,title,slug,summary,neighborhood,source_name,source_url,source_url_hash,fingerprint,author_name,image_url,image_credit,image_license,address,event_start_at,event_end_at,ticket_price,published_at,last_verified_at,verification_status,status,requires_human_approval,created_at)
            SELECT old.id,IF(old.event_at IS NULL,'News','Event'),
              CASE old.category WHEN 'cultura' THEN 'Cultura' WHEN 'familia' THEN 'Familia' WHEN 'infantil' THEN 'Familia' WHEN 'esporte' THEN 'Esporte' WHEN 'politica' THEN 'Politica' WHEN 'transporte' THEN 'Mobilidade' WHEN 'mobilidade' THEN 'Mobilidade' WHEN 'saude' THEN 'Servicos' WHEN 'servicos' THEN 'Servicos' WHEN 'turismo' THEN 'Turismo' WHEN 'cinema' THEN 'Cultura' WHEN 'teatro' THEN 'Cultura' ELSE 'Cidades' END,
              old.title,CONCAT('legado-',old.id),old.summary,old.neighborhood,old.source_name,old.source_url,old.source_url_hash,SHA2(CONCAT(old.title,old.summary),256),old.author_name,old.image_url,old.image_credit,old.image_license,old.address,old.event_at,old.end_at,old.ticket_price,old.published_at,old.last_verified_at,
              CASE old.verification_status WHEN 'verified' THEN 'Verified' WHEN 'outdated' THEN 'Outdated' ELSE 'Pending' END,
              CASE old.status WHEN 'published' THEN 'Published' WHEN 'archived' THEN 'Archived' WHEN 'review' THEN 'InReview' ELSE 'Draft' END,1,old.created_at
            FROM content_items old WHERE NOT EXISTS (SELECT 1 FROM mb_content_items current WHERE current.source_url_hash=old.source_url_hash);
            INSERT IGNORE INTO mb_content_revisions (content_id,revision,change_summary,snapshot)
            SELECT id,1,'Importação de conteúdo legado',JSON_OBJECT('title',title,'sourceUrl',source_url,'status',status) FROM mb_content_items WHERE slug=CONCAT('legado-',id);
            INSERT INTO mb_ingestion_candidates (source_id,source_name,title,summary,source_url,source_url_hash,fingerprint,published_at,status,content_id)
            SELECT ns.id,ns.name,old.title,old.summary,old.source_url,old.source_url_hash,SHA2(CONCAT(old.title,old.summary),256),old.published_at,
              CASE old.status WHEN 'rejected' THEN 'Rejected' WHEN 'approved' THEN 'Approved' ELSE 'Pending' END,content.id
            FROM ingestion_candidates old JOIN ingestion_sources os ON os.id=old.source_id JOIN mb_ingestion_sources ns ON ns.feed_url_hash=os.feed_url_hash
            LEFT JOIN mb_content_items content ON content.source_url_hash=old.source_url_hash
            WHERE NOT EXISTS (SELECT 1 FROM mb_ingestion_candidates current WHERE current.source_url_hash=old.source_url_hash);
            INSERT INTO mb_audit_logs (action,entity_type,details) VALUES ('legacy.imported','database',JSON_OBJECT('mode','additive','originalTablesPreserved',true));
            """, transaction: tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }
}
