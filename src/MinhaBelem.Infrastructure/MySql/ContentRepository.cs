using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Core.Text;
using MinhaBelem.Core.Policies;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class ContentRepository(MySqlDataSource dataSource) : IContentRepository
{
    private const string Select = """
        SELECT id AS Id, kind AS Kind, category AS Category, subject AS Subject, title AS Title, slug AS Slug,
               summary AS Summary, body AS Body, neighborhood AS Neighborhood, municipality AS Municipality,
               state AS State, address AS Address, latitude AS Latitude, longitude AS Longitude,
               source_id AS SourceId, source_name AS SourceName, source_url AS SourceUrl,
               author_name AS AuthorName, source_published_at AS SourcePublishedAt,
               image_url AS ImageUrl, image_credit AS ImageCredit, image_license AS ImageLicense,
               event_start_at AS EventStartAt, event_end_at AS EventEndAt, ticket_price AS TicketPrice,
               ticket_price_text AS TicketPriceText, is_free AS IsFree, status AS Status,
               verification_status AS VerificationStatus, flags AS Flags, duplicate_of_id AS DuplicateOfId,
               requires_human_approval AS RequiresHumanApproval, published_at AS PublishedAt,
               expires_at AS ExpiresAt, last_verified_at AS LastVerifiedAt, reviewed_by_id AS ReviewedById,
               created_by_id AS CreatedById, revision AS Revision, created_at AS CreatedAt, updated_at AS UpdatedAt
        FROM mb_content_items
        """;

    private async Task<MySqlConnection> OpenAsync(CancellationToken ct)
        => (MySqlConnector.MySqlConnection)await dataSource.OpenConnectionAsync(ct);

    public async Task<IReadOnlyList<ContentItem>> SearchAsync(ContentQuery query, CancellationToken ct)
        => await SearchInternalAsync(query, true, ct);

    public Task<IReadOnlyList<ContentItem>> EditorialAsync(ContentQuery query, CancellationToken ct)
        => SearchInternalAsync(query, false, ct);

    private async Task<IReadOnlyList<ContentItem>> SearchInternalAsync(ContentQuery query, bool publicOnly, CancellationToken ct)
    {
        var (where, parameters) = BuildFilter(query, publicOnly);
        var order = query.Sort switch
        {
            ContentSort.Upcoming => "COALESCE(event_start_at, published_at) ASC",
            ContentSort.Oldest => "published_at ASC",
            _ => "published_at DESC"
        };
        var sql = $"""
            {Select} {where} ORDER BY {order} LIMIT @limit OFFSET @offset
            """;
        parameters.Add("limit", query.PageSize);
        parameters.Add("offset", (Math.Max(1, query.Page) - 1) * query.PageSize);

        await using var connection = await OpenAsync(ct);
        var rows = (await connection.QueryAsync<ContentItem>(new CommandDefinition(sql, parameters, cancellationToken: ct))).AsList();
        await LoadChildrenAsync(connection, rows, ct);
        return rows;
    }

    public async Task<int> CountAsync(ContentQuery query, CancellationToken ct)
    {
        var (where, parameters) = BuildFilter(query, publicOnly: true);
        await using var connection = await OpenAsync(ct);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM mb_content_items {where}", parameters, cancellationToken: ct));
    }

    private static (string Where, DynamicParameters Parameters) BuildFilter(ContentQuery query, bool publicOnly)
    {
        var clauses = new List<string>();
        var parameters = new DynamicParameters();

        if (publicOnly)
            clauses.Add("status IN ('Published','Cancelled') AND (expires_at IS NULL OR expires_at > UTC_TIMESTAMP(6)) AND verification_status <> 'Outdated'");
        else if (query.Status is not null)
        {
            clauses.Add("status = @status");
            parameters.Add("status", query.Status.Value.ToString());
        }

        if (query.Category is not null)
        {
            clauses.Add("category = @category");
            parameters.Add("category", query.Category.Value.ToString());
        }
        if (!string.IsNullOrWhiteSpace(query.Subject))
        {
            clauses.Add("subject = @subject");
            parameters.Add("subject", query.Subject.Trim());
        }
        if (!string.IsNullOrWhiteSpace(query.Neighborhood))
        {
            clauses.Add("neighborhood = @neighborhood");
            parameters.Add("neighborhood", query.Neighborhood.Trim());
        }
        if (!string.IsNullOrWhiteSpace(query.Municipality))
        {
            clauses.Add("municipality = @municipality");
            parameters.Add("municipality", query.Municipality.Trim());
        }
        if (query.FreeOnly == true)
            clauses.Add("is_free = 1");
        if (query.Audience is not null)
        {
            clauses.Add("EXISTS (SELECT 1 FROM mb_content_audiences ca WHERE ca.content_id = mb_content_items.id AND ca.audience = @audience)");
            parameters.Add("audience", query.Audience.Value.ToString());
        }
        if (query.Accessibility is not null)
        {
            clauses.Add("EXISTS (SELECT 1 FROM mb_content_accessibility ax WHERE ax.content_id = mb_content_items.id AND ax.feature = @feature)");
            parameters.Add("feature", query.Accessibility.Value.ToString());
        }
        if (query.DateFrom is not null)
        {
            clauses.Add("DATE(COALESCE(event_start_at, published_at)) >= @dateFrom");
            parameters.Add("dateFrom", query.DateFrom.Value.ToDateTime(TimeOnly.MinValue));
        }
        if (query.DateTo is not null)
        {
            clauses.Add("DATE(COALESCE(event_start_at, published_at)) <= @dateTo");
            parameters.Add("dateTo", query.DateTo.Value.ToDateTime(TimeOnly.MinValue));
        }
        if (query.OnlyUpcomingEvents)
            clauses.Add("event_start_at >= UTC_TIMESTAMP(6)");

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            if (term.Length > 120) term = term[..120];
            clauses.Add("(title LIKE @search OR summary LIKE @search OR neighborhood LIKE @search OR municipality LIKE @search OR subject LIKE @search)");
            parameters.Add("search", $"%{EscapeLike(term)}%");
        }

        return (clauses.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", clauses), parameters);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("%", "\\%", StringComparison.Ordinal)
             .Replace("_", "\\_", StringComparison.Ordinal);

    public async Task<ContentItem?> GetAsync(long id, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        var item = await connection.QuerySingleOrDefaultAsync<ContentItem>(
            new CommandDefinition($"{Select} WHERE id = @id", new { id }, cancellationToken: ct));
        if (item is not null) await LoadChildrenAsync(connection, [item], ct);
        return item;
    }

    public async Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        var item = await connection.QuerySingleOrDefaultAsync<ContentItem>(
            new CommandDefinition($"{Select} WHERE slug = @slug LIMIT 1", new { slug }, cancellationToken: ct));
        if (item is not null) await LoadChildrenAsync(connection, [item], ct);
        return item;
    }

    private static async Task LoadChildrenAsync(MySqlConnector.MySqlConnection connection, IList<ContentItem> items, CancellationToken ct)
    {
        if (items.Count == 0) return;
        var ids = items.Select(i => i.Id).ToArray();
        var evidenceRows = (await connection.QueryAsync<(long Id, string Evidence)>(new CommandDefinition("SELECT content_id AS Id,evidence AS Evidence FROM mb_event_evidence WHERE content_id IN @ids", new { ids }, cancellationToken: ct))).ToDictionary(x => x.Id, x => x.Evidence);
        var audiences = await connection.QueryAsync<(long ContentId, string Audience)>(new CommandDefinition(
            "SELECT content_id AS ContentId, audience AS Audience FROM mb_content_audiences WHERE content_id IN @ids", new { ids }, cancellationToken: ct));
        var features = await connection.QueryAsync<(long ContentId, string Feature)>(new CommandDefinition(
            "SELECT content_id AS ContentId, feature AS Feature FROM mb_content_accessibility WHERE content_id IN @ids", new { ids }, cancellationToken: ct));

        var byAudience = audiences.Where(a => Enum.TryParse<Audience>(a.Audience, true, out _))
                                   .GroupBy(a => a.ContentId)
                                   .ToDictionary(g => g.Key, g => (IReadOnlyList<Audience>)g.Select(x => Enum.Parse<Audience>(x.Audience, true)).ToArray());
        var byFeature = features.Where(a => Enum.TryParse<AccessibilityFeature>(a.Feature, true, out _))
                                 .GroupBy(a => a.ContentId)
                                 .ToDictionary(g => g.Key, g => (IReadOnlyList<AccessibilityFeature>)g.Select(x => Enum.Parse<AccessibilityFeature>(x.Feature, true)).ToArray());

        foreach (var item in items)
        {
            if (evidenceRows.TryGetValue(item.Id, out var evidence)) item.EventDetails = System.Text.Json.JsonSerializer.Deserialize<Core.Services.EventEvidence>(evidence);
            if (byAudience.TryGetValue(item.Id, out var a)) item.Audiences = a;
            if (byFeature.TryGetValue(item.Id, out var f)) item.Accessibility = f;
        }
    }

    public async Task<long> InsertAsync(ContentItem item, long? actorId, string changeSummary, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        item.CreatedAt = item.CreatedAt == default ? now : item.CreatedAt;
        item.UpdatedAt = now;
        item.LastVerifiedAt = item.LastVerifiedAt == default ? now : item.LastVerifiedAt;
        item.PublishedAt = item.PublishedAt == default ? now : item.PublishedAt;
        item.CreatedById ??= actorId;

        await using var connection = await OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var id = (long)await connection.ExecuteScalarAsync<ulong>(new CommandDefinition(
            """
            INSERT INTO mb_content_items
              (kind, category, subject, title, slug, summary, body, neighborhood, municipality, state,
               address, latitude, longitude, source_name, source_url, source_url_hash, fingerprint,
               author_name, source_published_at, image_url, image_credit, image_license,
               event_start_at, event_end_at, ticket_price, ticket_price_text, is_free, status,
               verification_status, flags, duplicate_of_id, requires_human_approval, published_at,
               expires_at, last_verified_at, reviewed_by_id, created_by_id, revision)
            VALUES
              (@Kind, @Category, @Subject, @Title, @Slug, @Summary, @Body, @Neighborhood, @Municipality, @State,
               @Address, @Latitude, @Longitude, @SourceName, @SourceUrl, @SourceUrlHash, @Fingerprint,
               @AuthorName, @SourcePublishedAt, @ImageUrl, @ImageCredit, @ImageLicense,
               @EventStartAt, @EventEndAt, @TicketPrice, @TicketPriceText, @IsFree, @Status,
               @VerificationStatus, @Flags, @DuplicateOfId, @RequiresHumanApproval, @PublishedAt,
               @ExpiresAt, @LastVerifiedAt, @ReviewedById, @CreatedById, @Revision);
            SELECT LAST_INSERT_ID();
            """, ToParams(item), transaction, cancellationToken: ct));

        item.Id = checked((long)id);
        await ReplaceChildrenAsync(connection, item, transaction, ct);
        await WriteRevisionAsync(connection, item, actorId, changeSummary, transaction, ct);
        await transaction.CommitAsync(ct);
        return item.Id;
    }

    public async Task<bool> UpdateAsync(ContentItem item, long? actorId, string changeSummary, CancellationToken ct)
    {
        item.UpdatedAt = DateTime.UtcNow;
        await using var connection = await OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE mb_content_items SET
                kind = @Kind, category = @Category, subject = @Subject, title = @Title, slug = @Slug,
                summary = @Summary, body = @Body, neighborhood = @Neighborhood, municipality = @Municipality,
                address = @Address, latitude = @Latitude, longitude = @Longitude,
                source_name = @SourceName, source_url = @SourceUrl, source_url_hash = @SourceUrlHash,
                fingerprint = @Fingerprint, author_name = @AuthorName, source_published_at = @SourcePublishedAt,
                image_url = @ImageUrl, image_credit = @ImageCredit, image_license = @ImageLicense,
                event_start_at = @EventStartAt, event_end_at = @EventEndAt, ticket_price = @TicketPrice,
                ticket_price_text = @TicketPriceText, is_free = @IsFree, status = @Status,
                verification_status = @VerificationStatus, flags = @Flags,
                requires_human_approval = @RequiresHumanApproval, expires_at = @ExpiresAt,
                published_at = @PublishedAt, last_verified_at = @LastVerifiedAt, reviewed_by_id = @ReviewedById,
                revision = revision + 1, updated_at = UTC_TIMESTAMP(6)
            WHERE id = @Id AND revision = @Revision
            """, ToParams(item), transaction, cancellationToken: ct));

        if (affected == 0)
        {
            await transaction.RollbackAsync(ct);
            return false;
        }

        await ReplaceChildrenAsync(connection, item, transaction, ct);
        item.Revision += 1;
        await WriteRevisionAsync(connection, item, actorId, changeSummary, transaction, ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private static async Task ReplaceChildrenAsync(MySqlConnector.MySqlConnection connection, ContentItem item, MySqlConnector.MySqlTransaction tx, CancellationToken ct)
    {
        if (item.EventDetails is not null)
            await connection.ExecuteAsync(new CommandDefinition("INSERT INTO mb_event_evidence (content_id,evidence) VALUES (@id,@evidence) ON DUPLICATE KEY UPDATE evidence=VALUES(evidence)", new { id = item.Id, evidence = System.Text.Json.JsonSerializer.Serialize(item.EventDetails) }, tx, cancellationToken: ct));
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM mb_content_audiences WHERE content_id = @id; DELETE FROM mb_content_accessibility WHERE content_id = @id;",
            new { id = item.Id }, tx, cancellationToken: ct));
        if (item.Audiences.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO mb_content_audiences (content_id, audience) VALUES (@id, @audience);",
                item.Audiences.Select(a => new { id = item.Id, audience = a.ToString() }), tx, cancellationToken: ct));
        if (item.Accessibility.Count > 0)
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO mb_content_accessibility (content_id, feature) VALUES (@id, @feature);",
                item.Accessibility.Select(f => new { id = item.Id, feature = f.ToString() }), tx, cancellationToken: ct));
    }

    private static async Task WriteRevisionAsync(MySqlConnector.MySqlConnection connection, ContentItem item, long? actorId, string summary, MySqlConnector.MySqlTransaction tx, CancellationToken ct)
    {
        var snapshot = System.Text.Json.JsonSerializer.Serialize(item);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO mb_content_revisions (content_id, revision, change_summary, snapshot, changed_by_id, changed_at)
            VALUES (@id, @revision, @summary, @snapshot, @actor, UTC_TIMESTAMP(6))
            ON DUPLICATE KEY UPDATE change_summary = VALUES(change_summary), snapshot = VALUES(snapshot)
            """,
            new { id = item.Id, revision = item.Revision, summary = HtmlText.Truncate(summary, 480), snapshot, actor = actorId },
            tx, cancellationToken: ct));
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO mb_audit_logs (actor_id, action, entity_type, entity_id, details) VALUES (@actor, 'content.revised', 'content', @id, @details)",
            new { actor = actorId, id = item.Id, details = System.Text.Json.JsonSerializer.Serialize(new { revision = item.Revision, summary }) }, tx, cancellationToken: ct));
    }

    private static object ToParams(ContentItem item) => new
    {
        item.Id,
        Kind = item.Kind.ToString(),
        Category = item.Category.ToString(),
        item.Subject, item.Title, item.Slug, item.Summary, item.Body,
        item.Neighborhood, item.Municipality, State = item.State,
        item.Address, item.Latitude, item.Longitude,
        item.SourceName, item.SourceUrl,
        SourceUrlHash = item.SourceUrlHash ?? Hashing.UrlHash(item.SourceUrl),
        Fingerprint = Hashing.ContentFingerprint(item.Title, item.Summary),
        item.AuthorName, SourcePublishedAt = item.SourcePublishedAt,
        item.ImageUrl, item.ImageCredit, item.ImageLicense,
        item.EventStartAt, item.EventEndAt, item.TicketPrice, item.TicketPriceText,
        IsFree = item.IsFree,
        Status = item.Status.ToString(),
        VerificationStatus = item.VerificationStatus.ToString(),
        Flags = (int)item.Flags,
        item.DuplicateOfId,
        RequiresHumanApproval = item.RequiresHumanApproval,
        item.PublishedAt, item.ExpiresAt, item.LastVerifiedAt, item.ReviewedById,
        item.CreatedById, item.Revision
    };

    public async Task<bool> SetStatusAsync(long id, ContentStatus status, long actorId, CancellationToken ct)
    {
        var item = await GetAsync(id, ct);
        if (item is null) return false;
        if (status == ContentStatus.Published)
        {
            if (item.Status != ContentStatus.InReview || item.VerificationStatus != VerificationStatus.Verified ||
                string.IsNullOrWhiteSpace(item.AuthorName) || string.IsNullOrWhiteSpace(item.SourceName) ||
                !Core.Security.SafeUrl.Validate(item.SourceUrl).IsValid || item.DuplicateOfId is > 0 ||
                item.Flags.HasFlag(ModerationFlag.Outdated) || item.IsExpired(DateTime.UtcNow) ||
                (item.Kind == ContentKind.Event && item.EventStartAt is null) ||
                !PublicationPolicy.Evaluate(item, DateTime.UtcNow, automated: false).Allowed) return false;
            item.PublishedAt = DateTime.UtcNow;
            item.RequiresHumanApproval = false;
        }
        item.Status = status;
        item.ReviewedById = actorId;
        return await UpdateAsync(item, actorId, $"Estado editorial: {status}", ct);
    }

    public async Task<bool> DeleteAsync(long id, long actorId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        var item = await connection.QuerySingleOrDefaultAsync<ContentItem>(
            new CommandDefinition($"{Select} WHERE id = @id", new { id }, cancellationToken: ct));
        if (item is null) return false;

        // O histórico de auditoria e as revisões ficam registrados mesmo após a exclusão.
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO mb_audit_logs (actor_id, action, entity_type, entity_id, details) VALUES (@actor, 'content.deleted', 'content', @id, @details);",
            new { actor = actorId, id, details = System.Text.Json.JsonSerializer.Serialize(new { item.Title, item.SourceUrl, item.Slug }) },
            cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM mb_content_items WHERE id = @id", new { id }, cancellationToken: ct));
        return true;
    }

    public async Task<IReadOnlyList<ContentRevision>> RevisionsAsync(long contentId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        return (await connection.QueryAsync<ContentRevision>(new CommandDefinition(
            """
            SELECT id AS Id, content_id AS ContentId, revision AS Revision, change_summary AS ChangeSummary,
                   snapshot AS SnapshotJson, changed_by_id AS ChangedById, changed_by_name AS ChangedByName,
                   changed_at AS ChangedAt
            FROM mb_content_revisions WHERE content_id = @contentId ORDER BY revision DESC
            """, new { contentId }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyDictionary<long, string>> FingerprintsAsync(IEnumerable<long> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToArray();
        if (list.Length == 0) return new Dictionary<long, string>();
        await using var connection = await OpenAsync(ct);
        var rows = await connection.QueryAsync<(long Id, string Fingerprint)>(new CommandDefinition(
            "SELECT id AS Id, fingerprint AS Fingerprint FROM mb_content_items WHERE id IN @ids", new { ids = list }, cancellationToken: ct));
        return rows.ToDictionary(r => r.Id, r => r.Fingerprint);
    }

    public async Task<IReadOnlyList<ContentItem>> SearchForDuplicatesAsync(string title, string summary, int limit, CancellationToken ct)
    {
        var terms = TextNormalizer.SignificantTokens(title).Take(12).ToArray();
        if (terms.Length == 0) return Array.Empty<ContentItem>();

        await using var connection = await OpenAsync(ct);
        var rows = (await connection.QueryAsync<ContentItem>(new CommandDefinition(
            $"""
             {Select}
             WHERE status <> 'Archived'
               AND (title LIKE @like OR fingerprint = @fingerprint)
             ORDER BY published_at DESC LIMIT @limit
             """,
            new
            {
                like = $"%{EscapeLike(TextNormalizer.SignificantTokens(title).FirstOrDefault() ?? "___")}%",
                fingerprint = Hashing.ContentFingerprint(title, summary),
                limit = Math.Clamp(limit, 1, 50)
            }, cancellationToken: ct))).AsList();
        return rows;
    }

    public async Task<int> ExpireOldContentAsync(DateTime now, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE mb_content_items
            SET status = 'Archived', updated_at = UTC_TIMESTAMP(6)
            WHERE status = 'Published' AND expires_at IS NOT NULL AND expires_at <= @now
            """, new { now }, cancellationToken: ct));
    }
}
