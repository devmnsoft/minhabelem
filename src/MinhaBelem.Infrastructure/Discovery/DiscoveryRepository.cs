using System.Text.Json;
using Dapper;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Infrastructure.Discovery;

public sealed class DiscoveryRepository(MySqlDataSource data) : IDiscoveryRepository
{
    public async Task<SourceProfile> ProfileAsync(long sourceId, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        return await db.QuerySingleOrDefaultAsync<SourceProfile>(new CommandDefinition("SELECT source_id AS SourceId,owner_name AS OwnerName,organization AS Organization,region AS Region,keywords AS Keywords,frequency_minutes AS FrequencyMinutes,daily_request_limit AS DailyRequestLimit,max_items AS MaxItems,credential_reference AS CredentialReference,checkpoint AS Checkpoint,status AS Status,next_due_at AS NextDueAt,records_found AS RecordsFound FROM mb_source_profiles WHERE source_id=@sourceId", new { sourceId }, cancellationToken: ct)) ?? new SourceProfile { SourceId = sourceId };
    }
    public async Task SaveProfileAsync(SourceProfile profile, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("""
            INSERT INTO mb_source_profiles (source_id,owner_name,organization,region,keywords,frequency_minutes,daily_request_limit,max_items,credential_reference)
            VALUES (@SourceId,@OwnerName,@Organization,@Region,@Keywords,@FrequencyMinutes,@DailyRequestLimit,@MaxItems,@CredentialReference)
            ON DUPLICATE KEY UPDATE owner_name=VALUES(owner_name),organization=VALUES(organization),region=VALUES(region),keywords=VALUES(keywords),frequency_minutes=VALUES(frequency_minutes),daily_request_limit=VALUES(daily_request_limit),max_items=VALUES(max_items),credential_reference=VALUES(credential_reference)
            """, profile, cancellationToken: ct));
    }
    public async Task<bool> ReserveRequestAsync(long sourceId, int dailyLimit, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("INSERT IGNORE INTO mb_source_requests (source_id,day,requests) VALUES (@sourceId,UTC_DATE(),0)", new { sourceId }, cancellationToken: ct));
        return await db.ExecuteAsync(new CommandDefinition("UPDATE mb_source_requests SET requests=requests+1 WHERE source_id=@sourceId AND day=UTC_DATE() AND requests<@dailyLimit", new { sourceId, dailyLimit }, cancellationToken: ct)) == 1;
    }
    public async Task FinishAsync(long sourceId, string status, DateTime nextDue, int records, string? checkpoint, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("UPDATE mb_source_profiles SET status=@status,next_due_at=@nextDue,records_found=records_found+@records,checkpoint=COALESCE(@checkpoint,checkpoint) WHERE source_id=@sourceId", new { sourceId, status, nextDue, records, checkpoint }, cancellationToken: ct));
    }
    public async Task<(long Id, bool Changed)> StoreAsync(IngestionSource source, DiscoveredItem item, long runId, CancellationToken ct)
    {
        var snapshot = JsonSerializer.Serialize(item);
        var fingerprint = Hashing.ContentFingerprint(item.Title, item.Summary + JsonSerializer.Serialize(item.Event) + item.MediaUrl + item.MediaLicense + item.MediaCredit);
        await using var db = await data.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var hash = item.ExternalId is null ? Hashing.UrlHash(item.Url) : Hashing.Sha256(Hashing.NormalizeUrl(item.Url) + "|" + item.ExternalId);
        var old = await db.QuerySingleOrDefaultAsync<(long Id, string Fingerprint)?>(new CommandDefinition("SELECT id AS Id,fingerprint AS Fingerprint FROM mb_ingestion_candidates WHERE source_url_hash=@hash FOR UPDATE", new { hash }, tx, cancellationToken: ct));
        if (old is { } same && same.Fingerprint == fingerprint) return (same.Id, false);
        long id;
        if (old is { } changed)
        {
            id = changed.Id;
            await db.ExecuteAsync(new CommandDefinition("UPDATE mb_ingestion_candidates SET title=@title,summary=@summary,fingerprint=@fingerprint,status='Pending',review_notes='Alteração detectada na fonte; exige conferência',updated_at=UTC_TIMESTAMP(6) WHERE id=@id", new { id, title = HtmlText.Clean(item.Title, 300), summary = HtmlText.ToPlainText(item.Summary, 1500), fingerprint }, tx, cancellationToken: ct));
        }
        else
        {
            var duplicate = await db.ExecuteScalarAsync<long?>(new CommandDefinition("SELECT id FROM mb_ingestion_candidates WHERE fingerprint=@fingerprint LIMIT 1", new { fingerprint }, tx, cancellationToken: ct));
            id = await db.ExecuteScalarAsync<long>(new CommandDefinition("""
                INSERT INTO mb_ingestion_candidates (source_id,source_name,run_id,title,summary,source_url,source_url_hash,fingerprint,author_name,published_at,status,duplicate_of_id)
                VALUES (@SourceId,@Name,@runId,@title,@summary,@url,@hash,@fingerprint,@author,@posted,@status,@duplicate); SELECT LAST_INSERT_ID();
                """, new { SourceId = source.Id, source.Name, runId = runId > 0 ? (long?)runId : null, title = HtmlText.Clean(item.Title, 300), summary = HtmlText.ToPlainText(item.Summary, 1500), url = item.Url, hash, fingerprint, author = HtmlText.Clean(item.Author, 240), posted = item.PostedAt ?? DateTime.UtcNow, status = duplicate is null ? "Pending" : "Duplicate", duplicate }, tx, cancellationToken: ct));
        }
        await db.ExecuteAsync(new CommandDefinition("INSERT IGNORE INTO mb_discovery_versions (candidate_id,fingerprint,snapshot) VALUES (@id,@fingerprint,@snapshot)", new { id, fingerprint, snapshot }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return (id, true);
    }
    public async Task<EventEvidence?> EvidenceAsync(long candidateId, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        var snapshot = await db.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT snapshot FROM mb_discovery_versions WHERE candidate_id=@candidateId ORDER BY id DESC LIMIT 1", new { candidateId }, cancellationToken: ct));
        return snapshot is null ? null : JsonSerializer.Deserialize<DiscoveredItem>(snapshot)?.Event;
    }
}
