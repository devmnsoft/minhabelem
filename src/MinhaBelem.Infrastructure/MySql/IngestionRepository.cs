using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class IngestionRepository(MySqlDataSource dataSource) : IIngestionRepository
{
    private const string CandidateSelect = """
        SELECT id AS Id, source_id AS SourceId, source_name AS SourceName, run_id AS RunId,
               external_id AS ExternalId, title AS Title, summary AS Summary, source_url AS SourceUrl,
               author_name AS AuthorName, published_at AS PublishedAt, image_url AS ImageUrl,
               image_credit AS ImageCredit, status AS Status, duplicate_of_id AS DuplicateOfId,
               content_id AS ContentId, review_notes AS ReviewNotes,
               created_at AS CreatedAt, updated_at AS UpdatedAt
        FROM mb_ingestion_candidates
        """;

    public async Task<IReadOnlyList<IngestionSource>> SourcesAsync(bool onlyOperational, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var sql = $"""
            SELECT id AS Id, name AS Name, feed_url AS FeedUrl, protocol AS Protocol, topics AS Topics,
                   municipality AS Municipality, enabled AS Enabled, approved AS Approved,
                   respect_robots AS RespectRobots, license_note AS LicenseNote,
                   last_checked_at AS LastCheckedAt, last_success_at AS LastSuccessAt,
                   last_error AS LastError, consecutive_failures AS ConsecutiveFailures,
                   etag AS ETag, last_modified AS LastModified, created_at AS CreatedAt
            FROM mb_ingestion_sources
            {(onlyOperational ? "WHERE enabled = 1 AND approved = 1" : string.Empty)}
            ORDER BY name
            """;
        return (await connection.QueryAsync<IngestionSource>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    public async Task<IngestionSource?> SourceAsync(long id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<IngestionSource>(new CommandDefinition(
            """
            SELECT id AS Id, name AS Name, feed_url AS FeedUrl, protocol AS Protocol, topics AS Topics,
                   municipality AS Municipality, enabled AS Enabled, approved AS Approved,
                   respect_robots AS RespectRobots, license_note AS LicenseNote,
                   last_checked_at AS LastCheckedAt, last_success_at AS LastSuccessAt,
                   last_error AS LastError, consecutive_failures AS ConsecutiveFailures,
                   etag AS ETag, last_modified AS LastModified, created_at AS CreatedAt
            FROM mb_ingestion_sources WHERE id = @id
            """, new { id }, cancellationToken: ct));
    }

    public async Task<long> UpsertSourceAsync(IngestionSource source, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var hash = Hashing.UrlHash(source.FeedUrl);
        var id = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            """
            INSERT INTO mb_ingestion_sources
              (name, feed_url, feed_url_hash, protocol, topics, municipality, enabled, approved,
               respect_robots, license_note)
            VALUES (@Name, @FeedUrl, @Hash, @Protocol, @Topics, @Municipality, @Enabled, @Approved,
                    @RespectRobots, @LicenseNote)
            ON DUPLICATE KEY UPDATE
              name = VALUES(name), protocol = VALUES(protocol), topics = VALUES(topics),
              municipality = VALUES(municipality), enabled = VALUES(enabled),
              approved = VALUES(approved), respect_robots = VALUES(respect_robots),
              license_note = VALUES(license_note), feed_url = VALUES(feed_url)
            """, new
            {
                source.Name, source.FeedUrl, Hash = hash,
                Protocol = source.Protocol.ToString(), source.Topics, source.Municipality,
                source.Enabled, source.Approved, source.RespectRobots, source.LicenseNote
            }, cancellationToken: ct));

        if (id is > 0) return id.Value;
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT id FROM mb_ingestion_sources WHERE feed_url_hash = @hash", new { hash }, cancellationToken: ct));
    }

    public async Task<bool> UpdateSourceRuntimeAsync(long id, DateTime checkedAt, DateTime? successAt, string? error,
        string? etag, DateTime? lastModified, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE mb_ingestion_sources SET
                last_checked_at = @checkedAt,
                last_success_at = COALESCE(@successAt, last_success_at),
                last_error = @error,
                consecutive_failures = CASE WHEN @successAt IS NULL THEN consecutive_failures + 1 ELSE 0 END,
                etag = COALESCE(@etag, etag),
                last_modified = COALESCE(@lastModified, last_modified)
            WHERE id = @id
            """,
            new { id, checkedAt, successAt, error = HtmlText.Truncate(error, 900), etag, lastModified },
            cancellationToken: ct)) > 0;
    }

    public async Task<long> StartRunAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "INSERT INTO mb_ingestion_runs (started_at, finished_at, status) VALUES (UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 'Running'); SELECT LAST_INSERT_ID();",
            cancellationToken: ct));
    }

    public async Task FinishRunAsync(IngestionRun run, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE mb_ingestion_runs SET finished_at = @FinishedAt, status = @Status,
                sources_checked = @SourcesChecked, sources_failed = @SourcesFailed,
                items_seen = @ItemsSeen, items_inserted = @ItemsInserted,
                items_duplicate = @ItemsDuplicate, error = @Error
            WHERE id = @Id
            """,
            new
            {
                run.Id, run.FinishedAt, Status = run.Status.ToString(), run.SourcesChecked, run.SourcesFailed,
                run.ItemsSeen, run.ItemsInserted, run.ItemsDuplicate,
                Error = HtmlText.Truncate(run.Error, 900)
            }, cancellationToken: ct));
    }

    public async Task<IngestionCandidate?> InsertCandidateAsync(IngestionCandidate candidate, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        candidate.CreatedAt = candidate.CreatedAt == default ? now : candidate.CreatedAt;
        candidate.UpdatedAt = now;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT IGNORE INTO mb_ingestion_candidates
              (source_id, source_name, run_id, external_id, title, summary, source_url, source_url_hash,
               fingerprint, author_name, published_at, image_url, image_credit, status, duplicate_of_id)
            VALUES
              (@SourceId, @SourceName, @RunId, @ExternalId, @Title, @Summary, @SourceUrl, @UrlHash,
               @Fingerprint, @AuthorName, @PublishedAt, @ImageUrl, @ImageCredit, @Status, @DuplicateOfId)
            """,
            new
            {
                candidate.SourceId, candidate.SourceName, candidate.RunId, candidate.ExternalId,
                candidate.Title, candidate.Summary, candidate.SourceUrl,
                UrlHash = Hashing.UrlHash(candidate.SourceUrl),
                Fingerprint = Hashing.ContentFingerprint(candidate.Title, candidate.Summary),
                candidate.AuthorName, candidate.PublishedAt, candidate.ImageUrl, candidate.ImageCredit,
                Status = candidate.Status.ToString(), candidate.DuplicateOfId
            }, cancellationToken: ct));

        if (affected > 0) return candidate;
        return await connection.QuerySingleOrDefaultAsync<IngestionCandidate>(
            new CommandDefinition($"{CandidateSelect} WHERE source_url_hash = @hash",
                new { hash = Hashing.UrlHash(candidate.SourceUrl) }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<IngestionCandidate>> CandidatesAsync(CandidateQuery query, CancellationToken ct)
    {
        var (where, parameters) = BuildCandidateFilter(query);
        parameters.Add("limit", query.PageSize);
        parameters.Add("offset", (Math.Max(1, query.Page) - 1) * query.PageSize);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return (await connection.QueryAsync<IngestionCandidate>(new CommandDefinition(
            $"{CandidateSelect} {where} ORDER BY published_at DESC, id DESC LIMIT @limit OFFSET @offset",
            parameters, cancellationToken: ct))).AsList();
    }

    public async Task<int> CountCandidatesAsync(CandidateQuery query, CancellationToken ct)
    {
        var (where, parameters) = BuildCandidateFilter(query);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM mb_ingestion_candidates {where}", parameters, cancellationToken: ct));
    }

    private static (string, DynamicParameters) BuildCandidateFilter(CandidateQuery query)
    {
        var clauses = new List<string>();
        var parameters = new DynamicParameters();
        if (query.Status is not null)
        {
            clauses.Add("status = @status");
            parameters.Add("status", query.Status.Value.ToString());
        }
        if (query.SourceId is not null)
        {
            clauses.Add("source_id = @sourceId");
            parameters.Add("sourceId", query.SourceId.Value);
        }
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            if (term.Length > 120) term = term[..120];
            clauses.Add("(title LIKE @search OR summary LIKE @search)");
            parameters.Add("search", $"%{term.Replace("%", "\\%", StringComparison.Ordinal)}%");
        }
        return (clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses), parameters);
    }

    public async Task<IngestionCandidate?> CandidateAsync(long id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<IngestionCandidate>(
            new CommandDefinition($"{CandidateSelect} WHERE id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<bool> SetCandidateStatusAsync(long id, CandidateStatus status, long? reviewerId, string? notes, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE mb_ingestion_candidates SET status = @status, review_notes = @notes, updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
            """,
            new { id, status = status.ToString(), notes = HtmlText.Truncate(notes, 900) },
            cancellationToken: ct)) > 0;
    }

    public async Task<IReadOnlyList<IngestionCandidate>> SearchForDuplicatesAsync(string title, string summary, string sourceUrl, int limit, CancellationToken ct)
    {
        var firstToken = TextNormalizer.SignificantTokens(title).FirstOrDefault();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var sql = firstToken is null
            ? $"{CandidateSelect} WHERE fingerprint = @fingerprint ORDER BY id DESC LIMIT @limit"
            : $"{CandidateSelect} WHERE source_url_hash = @urlHash OR fingerprint = @fingerprint OR title LIKE @like ORDER BY id DESC LIMIT @limit";
        return (await connection.QueryAsync<IngestionCandidate>(new CommandDefinition(sql, new
        {
            urlHash = Hashing.UrlHash(sourceUrl),
            fingerprint = Hashing.ContentFingerprint(title, summary),
            like = $"%{firstToken}%",
            limit = Math.Clamp(limit, 1, 50)
        }, cancellationToken: ct))).AsList();
    }
}