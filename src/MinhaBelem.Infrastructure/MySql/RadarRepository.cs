using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Text;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class RadarRepository(MySqlDataSource data) : IRadarRepository
{
    public async Task<RadarDashboardStats> GetDashboardStatsAsync(CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);

        var totalSources = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ingestion_sources", cancellationToken: ct));
        var activeSources = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM ingestion_sources WHERE enabled=TRUE AND approved=TRUE", cancellationToken: ct));
        var discoveredItems = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_ingestion_candidates", cancellationToken: ct));
        var pendingReview = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_ingestion_candidates WHERE status='Pending'", cancellationToken: ct));
        var duplicates = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_ingestion_candidates WHERE status='Duplicate'", cancellationToken: ct));
        var upcomingEvents = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_content_items WHERE kind='Event' AND event_start_at > UTC_TIMESTAMP(6)", cancellationToken: ct));
        var errors = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_radar_crawl_history WHERE status_code >= 400 OR error_message IS NOT NULL", cancellationToken: ct));
        var aiTokens = await db.ExecuteScalarAsync<long?>(new CommandDefinition("SELECT SUM(prompt_tokens + completion_tokens) FROM mb_ai_runs", cancellationToken: ct)) ?? 0L;
        var lastCrawl = await db.ExecuteScalarAsync<DateTime?>(new CommandDefinition("SELECT MAX(crawled_at) FROM mb_radar_crawl_history", cancellationToken: ct));
        var socialCount = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_radar_social_profiles WHERE is_active=TRUE", cancellationToken: ct));

        return new RadarDashboardStats
        {
            TotalSources = totalSources,
            ActiveSources = activeSources,
            DiscoveredItemsCount = discoveredItems,
            UpcomingEventsCount = upcomingEvents,
            PendingReviewCount = pendingReview,
            DuplicateItemsCount = duplicates,
            CrawlErrorsCount = errors,
            AiTokensConsumed = aiTokens,
            LastCrawlAt = lastCrawl,
            NextCrawlDueAt = lastCrawl?.AddMinutes(10) ?? DateTime.UtcNow.AddMinutes(10),
            SocialProfilesCount = socialCount
        };
    }

    public async Task<IReadOnlyList<DiscoveredSource>> DiscoveredSourcesAsync(DiscoveredSourceQuery query, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);

        var sql = """
            SELECT id AS Id, source_name AS SourceName, url AS Url, url_hash AS UrlHash,
                   category AS Category, municipality AS Municipality,
                   discovered_from_url AS DiscoveredFromUrl, evaluation_status AS EvaluationStatus,
                   evaluation_notes AS EvaluationNotes, feed_protocol_candidate AS FeedProtocolCandidate,
                   confidence_score AS ConfidenceScore, discovered_at AS DiscoveredAt,
                   evaluated_at AS EvaluatedAt, evaluated_by_id AS EvaluatedById
            FROM mb_radar_discovered_sources
            WHERE (@Status IS NULL OR evaluation_status = @Status)
              AND (@Category IS NULL OR category = @Category)
              AND (@Municipality IS NULL OR municipality = @Municipality)
              AND (@Search IS NULL OR source_name LIKE CONCAT('%', @Search, '%') OR url LIKE CONCAT('%', @Search, '%'))
            ORDER BY discovered_at DESC
            LIMIT @Limit OFFSET @Offset
            """;

        var statusString = query.Status?.ToString().ToLowerInvariant();
        var offset = (query.Page - 1) * query.PageSize;

        var items = await db.QueryAsync<DiscoveredSource>(new CommandDefinition(
            sql,
            new { Status = statusString, query.Category, query.Municipality, query.Search, Limit = query.PageSize, Offset = offset },
            cancellationToken: ct));

        return items.ToList();
    }

    public async Task<int> CountDiscoveredSourcesAsync(DiscoveredSourceQuery query, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);

        var sql = """
            SELECT COUNT(*)
            FROM mb_radar_discovered_sources
            WHERE (@Status IS NULL OR evaluation_status = @Status)
              AND (@Category IS NULL OR category = @Category)
              AND (@Municipality IS NULL OR municipality = @Municipality)
              AND (@Search IS NULL OR source_name LIKE CONCAT('%', @Search, '%') OR url LIKE CONCAT('%', @Search, '%'))
            """;

        var statusString = query.Status?.ToString().ToLowerInvariant();
        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            sql,
            new { Status = statusString, query.Category, query.Municipality, query.Search },
            cancellationToken: ct));
    }

    public async Task<long> InsertDiscoveredSourceAsync(DiscoveredSource source, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);

        var hash = source.UrlHash ?? Hashing.UrlHash(source.Url);
        var status = source.EvaluationStatus.ToString().ToLowerInvariant();

        var sql = """
            INSERT IGNORE INTO mb_radar_discovered_sources
            (source_name, url, url_hash, category, municipality, discovered_from_url, evaluation_status, evaluation_notes, feed_protocol_candidate, confidence_score)
            VALUES (@SourceName, @Url, @hash, @Category, @Municipality, @DiscoveredFromUrl, @status, @EvaluationNotes, @FeedProtocolCandidate, @ConfidenceScore);
            SELECT LAST_INSERT_ID();
            """;

        return await db.ExecuteScalarAsync<long>(new CommandDefinition(
            sql,
            new { source.SourceName, source.Url, hash, source.Category, source.Municipality, source.DiscoveredFromUrl, status, source.EvaluationNotes, source.FeedProtocolCandidate, source.ConfidenceScore },
            cancellationToken: ct));
    }

    public async Task<bool> EvaluateDiscoveredSourceAsync(long id, SourceEvaluationStatus status, string? notes, long? evaluatedById, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);

        var statusString = status.ToString().ToLowerInvariant();

        var rows = await db.ExecuteAsync(new CommandDefinition(
            "UPDATE mb_radar_discovered_sources SET evaluation_status=@statusString, evaluation_notes=COALESCE(@notes, evaluation_notes), evaluated_at=UTC_TIMESTAMP(6), evaluated_by_id=@evaluatedById WHERE id=@id",
            new { id, statusString, notes, evaluatedById }, tx, cancellationToken: ct));

        if (rows > 0 && status == SourceEvaluationStatus.Approved)
        {
            // Ao aprovar, promove para fonte de ingestão oficial
            var source = await db.QuerySingleOrDefaultAsync<DiscoveredSource>(new CommandDefinition(
                "SELECT source_name AS SourceName, url AS Url, feed_protocol_candidate AS FeedProtocolCandidate, category AS Category, municipality AS Municipality FROM mb_radar_discovered_sources WHERE id=@id",
                new { id }, tx, cancellationToken: ct));

            if (source is not null)
            {
                var hash = Hashing.UrlHash(source.Url);
                var protocol = source.FeedProtocolCandidate.ToLowerInvariant() switch
                {
                    "rss" => "rss",
                    "atom" => "atom",
                    "jsonld" => "api",
                    _ => "rss"
                };

                await db.ExecuteAsync(new CommandDefinition("""
                    INSERT IGNORE INTO ingestion_sources (source_name, feed_url, feed_url_hash, protocol, enabled, approved, license_note, created_at)
                    VALUES (@SourceName, @Url, @hash, @protocol, TRUE, TRUE, 'Aprovada via MinhaBelém Radar', UTC_TIMESTAMP(6));
                    """, new { source.SourceName, source.Url, hash, protocol }, tx, cancellationToken: ct));
            }
        }

        await tx.CommitAsync(ct);
        return rows > 0;
    }

    public async Task<IReadOnlyList<CrawlHistoryEntry>> RecentCrawlHistoryAsync(int limit, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);

        var sql = """
            SELECT id AS Id, source_id AS SourceId, url AS Url, url_hash AS UrlHash,
                   status_code AS StatusCode, etag AS ETag, last_modified AS LastModified,
                   content_hash AS ContentHash, discovered_items_count AS DiscoveredItemsCount,
                   events_identified_count AS EventsIdentifiedCount, depth AS Depth,
                   duration_ms AS DurationMs, error_message AS ErrorMessage, crawled_at AS CrawledAt
            FROM mb_radar_crawl_history
            ORDER BY crawled_at DESC
            LIMIT @limit
            """;

        var items = await db.QueryAsync<CrawlHistoryEntry>(new CommandDefinition(sql, new { limit }, cancellationToken: ct));
        return items.ToList();
    }

    public async Task<long> RecordCrawlHistoryAsync(CrawlHistoryEntry entry, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);

        var hash = entry.UrlHash ?? Hashing.UrlHash(entry.Url);

        var sql = """
            INSERT INTO mb_radar_crawl_history
            (source_id, url, url_hash, status_code, etag, last_modified, content_hash, discovered_items_count, events_identified_count, depth, duration_ms, error_message, crawled_at)
            VALUES (@SourceId, @Url, @hash, @StatusCode, @ETag, @LastModified, @ContentHash, @DiscoveredItemsCount, @EventsIdentifiedCount, @Depth, @DurationMs, @ErrorMessage, UTC_TIMESTAMP(6));
            SELECT LAST_INSERT_ID();
            """;

        return await db.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            entry.SourceId,
            entry.Url,
            hash,
            entry.StatusCode,
            entry.ETag,
            entry.LastModified,
            entry.ContentHash,
            entry.DiscoveredItemsCount,
            entry.EventsIdentifiedCount,
            entry.Depth,
            entry.DurationMs,
            entry.ErrorMessage
        }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SocialProfileRegistry>> SocialProfilesAsync(bool onlyActive, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);

        var sql = """
            SELECT id AS Id, platform AS Platform, profile_handle AS ProfileHandle,
                   display_name AS DisplayName, profile_url AS ProfileUrl,
                   official_embed_url AS OfficialEmbedUrl, category AS Category,
                   municipality AS Municipality, license_notes AS LicenseNotes,
                   is_active AS IsActive, last_checked_at AS LastCheckedAt, created_at AS CreatedAt
            FROM mb_radar_social_profiles
            WHERE (@onlyActive = FALSE OR is_active = TRUE)
            ORDER BY display_name ASC
            """;

        var items = await db.QueryAsync<SocialProfileRegistry>(new CommandDefinition(sql, new { onlyActive }, cancellationToken: ct));
        return items.ToList();
    }

    public async Task<long> UpsertSocialProfileAsync(SocialProfileRegistry profile, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);

        var platform = profile.Platform.ToString().ToLowerInvariant();

        var sql = """
            INSERT INTO mb_radar_social_profiles
            (platform, profile_handle, display_name, profile_url, official_embed_url, category, municipality, license_notes, is_active, created_at)
            VALUES (@platform, @ProfileHandle, @DisplayName, @ProfileUrl, @OfficialEmbedUrl, @Category, @Municipality, @LicenseNotes, @IsActive, UTC_TIMESTAMP(6))
            ON DUPLICATE KEY UPDATE
                display_name = VALUES(display_name),
                profile_url = VALUES(profile_url),
                official_embed_url = VALUES(official_embed_url),
                category = VALUES(category),
                municipality = VALUES(municipality),
                license_notes = VALUES(license_notes),
                is_active = VALUES(is_active);
            SELECT LAST_INSERT_ID();
            """;

        return await db.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            platform,
            profile.ProfileHandle,
            profile.DisplayName,
            profile.ProfileUrl,
            profile.OfficialEmbedUrl,
            profile.Category,
            profile.Municipality,
            profile.LicenseNotes,
            profile.IsActive
        }, cancellationToken: ct));
    }

    public async Task<bool> ToggleSocialProfileAsync(long id, bool isActive, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        var rows = await db.ExecuteAsync(new CommandDefinition(
            "UPDATE mb_radar_social_profiles SET is_active=@isActive WHERE id=@id",
            new { id, isActive }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<CrawlHistoryEntry?> GetLastCrawlForUrlAsync(string url, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        var hash = Hashing.UrlHash(url);

        return await db.QueryFirstOrDefaultAsync<CrawlHistoryEntry>(new CommandDefinition(
            "SELECT id AS Id, source_id AS SourceId, url AS Url, status_code AS StatusCode, etag AS ETag, last_modified AS LastModified, content_hash AS ContentHash FROM mb_radar_crawl_history WHERE url_hash=@hash ORDER BY crawled_at DESC LIMIT 1",
            new { hash }, cancellationToken: ct));
    }
}
