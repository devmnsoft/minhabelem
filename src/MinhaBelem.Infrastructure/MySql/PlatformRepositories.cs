using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class AuditRepository(MySqlDataSource dataSource) : IAuditRepository
{
    public async Task WriteAsync(AuditEntry entry, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO mb_audit_logs (actor_id, actor_name, action, entity_type, entity_id, details, ip_address, created_at)
            VALUES (@ActorId, @ActorName, @Action, @EntityType, @EntityId, @Details, @IpAddress, UTC_TIMESTAMP(6))
            """,
            new
            {
                entry.ActorId, entry.ActorName, entry.Action, entry.EntityType, entry.EntityId,
                Details = string.IsNullOrWhiteSpace(entry.DetailsJson) ? null : entry.DetailsJson,
                entry.IpAddress
            }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AuditEntry>> RecentAsync(int take, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return (await connection.QueryAsync<AuditEntry>(new CommandDefinition(
            """
            SELECT id AS Id, actor_id AS ActorId, actor_name AS ActorName, action AS Action,
                   entity_type AS EntityType, entity_id AS EntityId, details AS DetailsJson,
                   ip_address AS IpAddress, created_at AS CreatedAt
            FROM mb_audit_logs ORDER BY id DESC LIMIT @take
            """, new { take = Math.Clamp(take, 1, 200) }, cancellationToken: ct))).AsList();
    }
}

public sealed class AiRunRepository(MySqlDataSource dataSource) : IAiRunRepository
{
    public async Task<long> SaveAsync(AiRun run, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var id = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            """
            INSERT INTO mb_ai_runs
              (provider, model, model_version, task, target_type, target_id, prompt_chars,
               prompt_tokens, completion_tokens, cost_usd, latency_ms, success, error,
               response_json, triggered_by_user_id, created_at)
            VALUES
              (@Provider, @Model, @ModelVersion, @Task, @TargetType, @TargetId, @PromptChars,
               @PromptTokens, @CompletionTokens, @CostUsd, @LatencyMs, @Success, @Error,
               @ResponseJson, @TriggeredByUserId, UTC_TIMESTAMP(6));
            SELECT LAST_INSERT_ID();
            """,
            new
            {
                Provider = run.Provider.ToString(), run.Model, run.ModelVersion,
                Task = run.Task.ToString(), run.TargetType, run.TargetId, run.PromptChars,
                run.PromptTokens, run.CompletionTokens, run.CostUsd, run.LatencyMs, run.Success,
                Error = Core.Text.HtmlText.Truncate(run.Error, 900),
                run.ResponseJson, run.TriggeredByUserId
            }, cancellationToken: ct));
        return id ?? 0;
    }

    public async Task<IReadOnlyList<AiRun>> RecentAsync(int take, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return (await connection.QueryAsync<AiRun>(new CommandDefinition(
            """
            SELECT id AS Id, provider AS Provider, model AS Model, model_version AS ModelVersion,
                   task AS Task, target_type AS TargetType, target_id AS TargetId,
                   prompt_chars AS PromptChars, prompt_tokens AS PromptTokens,
                   completion_tokens AS CompletionTokens, cost_usd AS CostUsd, latency_ms AS LatencyMs,
                   success AS Success, error AS Error, response_json AS ResponseJson,
                   triggered_by_user_id AS TriggeredByUserId, created_at AS CreatedAt
            FROM mb_ai_runs ORDER BY id DESC LIMIT @take
            """, new { take = Math.Clamp(take, 1, 200) }, cancellationToken: ct))).AsList();
    }

    public async Task<decimal> CostSinceAsync(DateTime since, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT COALESCE(SUM(cost_usd), 0) FROM mb_ai_runs WHERE created_at >= @since",
            new { since }, cancellationToken: ct));
    }

    public async Task<(int Total, int Failed)> StatsSinceAsync(DateTime since, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<(int Total, int Failed)>(new CommandDefinition(
            "SELECT COUNT(*) AS Total, COALESCE(SUM(success = 0), 0) AS Failed FROM mb_ai_runs WHERE created_at >= @since",
            new { since }, cancellationToken: ct));
        return row;
    }
}

public sealed class CommunityRepository(MySqlDataSource dataSource) : ICommunityRepository
{
    public async Task<long> InsertAsync(CommunitySubmission submission, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        submission.CreatedAt = submission.CreatedAt == default ? now : submission.CreatedAt;
        submission.UpdatedAt = now;
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO mb_community_submissions (display_name, contact, message, status, created_at, updated_at)
            VALUES (@DisplayName, @Contact, @Message, @Status, @CreatedAt, @UpdatedAt);
            SELECT LAST_INSERT_ID();
            """,
            new
            {
                submission.DisplayName, submission.Contact, submission.Message,
                Status = submission.Status.ToString(), submission.CreatedAt, submission.UpdatedAt
            }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<CommunitySubmission>> SearchAsync(SubmissionStatus? status, int take, CancellationToken ct)
    {
        var parameters = new DynamicParameters();
        var where = string.Empty;
        if (status is not null)
        {
            where = "WHERE status = @status";
            parameters.Add("status", status.Value.ToString());
        }
        parameters.Add("take", Math.Clamp(take, 1, 200));
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return (await connection.QueryAsync<CommunitySubmission>(new CommandDefinition(
            $"""
             SELECT id AS Id, display_name AS DisplayName, contact AS Contact, message AS Message,
                    status AS Status, review_notes AS ReviewNotes, reviewed_by_id AS ReviewedById,
                    created_at AS CreatedAt, updated_at AS UpdatedAt
             FROM mb_community_submissions {where} ORDER BY created_at DESC LIMIT @take
             """, parameters, cancellationToken: ct))).AsList();
    }

    public async Task<bool> SetStatusAsync(long id, SubmissionStatus status, long reviewerId, string? notes, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE mb_community_submissions SET status = @status, review_notes = @notes,
                reviewed_by_id = @reviewerId, updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
            """,
            new { id, status = status.ToString(), notes = Core.Text.HtmlText.Truncate(notes, 900), reviewerId },
            cancellationToken: ct)) > 0;
    }
}

public sealed class AdvertiserRepository(MySqlDataSource dataSource) : IAdvertiserRepository
{
    public async Task<long> SubmitInquiryAsync(CommercialInquiry inquiry, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        return await db.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO mb_commercial_inquiries(company,contact_name,email,phone,plan,message)
            VALUES(@Company,@ContactName,@Email,@Phone,@Plan,@Message); SELECT LAST_INSERT_ID();
            """, inquiry, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<CommercialInquiry>> InquiriesAsync(CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        return (await db.QueryAsync<CommercialInquiry>(new CommandDefinition("""
            SELECT id,company,contact_name AS ContactName,email,phone,plan,message,handled,created_at AS CreatedAt
            FROM mb_commercial_inquiries ORDER BY handled,id DESC LIMIT 100
            """, cancellationToken: ct))).AsList();
    }
    public async Task<bool> HandleInquiryAsync(long id, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        return await db.ExecuteAsync(new CommandDefinition("UPDATE mb_commercial_inquiries SET handled=TRUE WHERE id=@id AND handled=FALSE", new { id }, cancellationToken: ct)) == 1;
    }
    private const string Select = """
        SELECT id AS Id, advertiser_name AS AdvertiserName, destination_url AS DestinationUrl,
               image_url AS ImageUrl, label AS Label, headline AS Headline, active AS Active,
               valid_from AS ValidFrom, valid_to AS ValidTo, advertiser_user_id AS AdvertiserUserId,
               created_at AS CreatedAt
        FROM mb_advertiser_campaigns
        """;

    public async Task<IReadOnlyList<AdvertiserCampaign>> ActiveAsync(DateTime now, int take, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return (await connection.QueryAsync<AdvertiserCampaign>(new CommandDefinition(
            $"""
             {Select}
             WHERE active = 1
               AND (valid_from IS NULL OR valid_from <= @now)
               AND (valid_to IS NULL OR valid_to >= @now)
             ORDER BY RAND() LIMIT @take
             """, new { now, take = Math.Clamp(take, 1, 10) }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<AdvertiserCampaign>> AllAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return (await connection.QueryAsync<AdvertiserCampaign>(new CommandDefinition(
            $"{Select} ORDER BY active DESC, created_at DESC", cancellationToken: ct))).AsList();
    }

    public async Task<long> UpsertAsync(AdvertiserCampaign campaign, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        if (campaign.Id > 0)
        {
            var changed = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE mb_advertiser_campaigns SET advertiser_name=@AdvertiserName,destination_url=@DestinationUrl,
                    image_url=@ImageUrl,label=@Label,headline=@Headline,active=@Active,valid_from=@ValidFrom,valid_to=@ValidTo
                WHERE id=@Id
                """, campaign, cancellationToken: ct));
            return changed > 0 ? campaign.Id : 0;
        }
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO mb_advertiser_campaigns
              (advertiser_name, destination_url, image_url, label, headline, active, valid_from, valid_to, advertiser_user_id)
            VALUES
              (@AdvertiserName, @DestinationUrl, @ImageUrl, @Label, @Headline, @Active, @ValidFrom, @ValidTo, @AdvertiserUserId)
            ON DUPLICATE KEY UPDATE
              advertiser_name = VALUES(advertiser_name), destination_url = VALUES(destination_url),
              image_url = VALUES(image_url), label = VALUES(label), headline = VALUES(headline),
              active = VALUES(active), valid_from = VALUES(valid_from), valid_to = VALUES(valid_to);
            SELECT LAST_INSERT_ID();
            """,
            new
            {
                campaign.AdvertiserName, campaign.DestinationUrl, campaign.ImageUrl,
                campaign.Label, campaign.Headline, campaign.Active,
                campaign.ValidFrom, campaign.ValidTo, campaign.AdvertiserUserId
            }, cancellationToken: ct));
    }

    public async Task<bool> SetActiveAsync(long id, bool active, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE mb_advertiser_campaigns SET active = @active WHERE id = @id", new { id, active }, cancellationToken: ct)) > 0;
    }
}

public sealed class DashboardRepository(MySqlDataSource dataSource) : IDashboardRepository
{
    public async Task<DashboardSnapshot> BuildAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        var totals = await connection.QuerySingleAsync<(int Published, int InReview, int Drafts)>(new CommandDefinition(
            """
            SELECT
              COALESCE(SUM(status = 'Published'), 0) AS Published,
              COALESCE(SUM(status = 'InReview'), 0)  AS InReview,
              COALESCE(SUM(status = 'Draft'), 0)     AS Drafts
            FROM mb_content_items
            """, cancellationToken: ct));

        var candidates = await connection.QuerySingleAsync<(int Pending, int Duplicate)>(new CommandDefinition(
            """
            SELECT COALESCE(SUM(status = 'Pending'), 0)  AS Pending,
                   COALESCE(SUM(status = 'Duplicate'), 0) AS Duplicate
            FROM mb_ingestion_candidates
            """, cancellationToken: ct));

        var sources = await connection.QuerySingleAsync<(int Active, int Failing)>(new CommandDefinition(
            """
            SELECT COALESCE(SUM(enabled = 1 AND approved = 1), 0) AS Active,
                   COALESCE(SUM(enabled = 1 AND approved = 1 AND consecutive_failures >= 3), 0) AS Failing
            FROM mb_ingestion_sources
            """, cancellationToken: ct));

        var misc = await connection.QuerySingleAsync<(int Places, int Submissions, int Campaigns, int Users, int LastRunFailed)>(new CommandDefinition(
            """
            SELECT
              (SELECT COUNT(*) FROM mb_places WHERE is_published = 1) AS Places,
              (SELECT COUNT(*) FROM mb_community_submissions WHERE status = 'Pending') AS Submissions,
              (SELECT COUNT(*) FROM mb_advertiser_campaigns WHERE active = 1) AS Campaigns,
              (SELECT COUNT(*) FROM mb_users WHERE enabled = 1) AS Users,
              (SELECT COALESCE(SUM(status <> 'Success'), 0) FROM mb_ingestion_runs
                WHERE started_at >= UTC_TIMESTAMP(6) - INTERVAL 1 DAY) AS LastRunFailed
            """, cancellationToken: ct));

        var aiSince = DateTime.UtcNow.AddHours(-24);
        var (aiTotal, aiFailed) = await connection.QuerySingleAsync<(int Total, int Failed)>(new CommandDefinition(
            "SELECT COUNT(*) AS Total, COALESCE(SUM(success = 0), 0) AS Failed FROM mb_ai_runs WHERE created_at >= @since",
            new { since = aiSince }, cancellationToken: ct));
        var cost = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT COALESCE(SUM(cost_usd), 0) FROM mb_ai_runs WHERE created_at >= @since",
            new { since = DateTime.UtcNow.AddDays(-30) }, cancellationToken: ct));

        var byDay = (await connection.QueryAsync<(DateOnly Day, int Count)>(new CommandDefinition(
            """
            SELECT DATE(published_at) AS Day, COUNT(*) AS Count
            FROM mb_content_items WHERE status = 'Published' AND published_at >= UTC_TIMESTAMP(6) - INTERVAL 14 DAY
            GROUP BY DATE(published_at) ORDER BY Day
            """, cancellationToken: ct))).AsList();

        var byCategory = (await connection.QueryAsync<(string Category, int Count)>(new CommandDefinition(
            """
            SELECT category AS Category, COUNT(*) AS Count
            FROM mb_content_items WHERE status = 'Published'
            GROUP BY category ORDER BY Count DESC
            """, cancellationToken: ct))).AsList();

        var audit = (await connection.QueryAsync<AuditEntry>(new CommandDefinition(
            """
            SELECT id AS Id, actor_id AS ActorId, actor_name AS ActorName, action AS Action,
                   entity_type AS EntityType, entity_id AS EntityId, details AS DetailsJson,
                   ip_address AS IpAddress, created_at AS CreatedAt
            FROM mb_audit_logs ORDER BY id DESC LIMIT 15
            """, cancellationToken: ct))).AsList();

        return new DashboardSnapshot
        {
            PublishedContent = totals.Published,
            ContentInReview = totals.InReview,
            ContentDrafts = totals.Drafts,
            PendingCandidates = candidates.Pending,
            DuplicateCandidates = candidates.Duplicate,
            ActiveSources = sources.Active,
            FailingSources = sources.Failing + misc.LastRunFailed,
            PublishedPlaces = misc.Places,
            PendingSubmissions = misc.Submissions,
            ActiveAdvertisers = misc.Campaigns,
            ActiveUsers = misc.Users,
            AiRunsLast24h = aiTotal,
            AiFailuresLast24h = aiFailed,
            AiCostUsdLast30d = cost,
            RecentAudit = audit,
            PublishedByDay = byDay.Select(r => new DailyPoint(r.Day, r.Count)).ToArray(),
            ByCategory = byCategory.Select(r => new CategoryPoint(r.Category, r.Count)).ToArray()
        };
    }
}
