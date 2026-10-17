using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class TranslationRepository(MySqlDataSource dataSource) : ITranslationRepository
{
    private async Task<MySqlConnector.MySqlConnection> OpenAsync(CancellationToken ct)
        => (MySqlConnector.MySqlConnection)await dataSource.OpenConnectionAsync(ct);

    public async Task<IReadOnlyList<Language>> GetLanguagesAsync(bool onlyEnabled, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var where = onlyEnabled ? "WHERE is_enabled = 1" : "";
        var sql = $"SELECT code AS Code, name AS Name, native_name AS NativeName, flag_emoji AS FlagEmoji, is_enabled AS IsEnabled, is_default AS IsDefault, requires_human_approval AS RequiresHumanApproval, display_order AS DisplayOrder, created_at AS CreatedAt, updated_at AS UpdatedAt FROM mb_languages {where} ORDER BY display_order ASC, name ASC";
        return (await db.QueryAsync<Language>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    public async Task<Language?> GetLanguageAsync(string code, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = "SELECT code AS Code, name AS Name, native_name AS NativeName, flag_emoji AS FlagEmoji, is_enabled AS IsEnabled, is_default AS IsDefault, requires_human_approval AS RequiresHumanApproval, display_order AS DisplayOrder, created_at AS CreatedAt, updated_at AS UpdatedAt FROM mb_languages WHERE code = @code LIMIT 1";
        return await db.QuerySingleOrDefaultAsync<Language>(new CommandDefinition(sql, new { code }, cancellationToken: ct));
    }

    public async Task<bool> UpdateLanguageAsync(Language language, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            UPDATE mb_languages
            SET name = @Name, native_name = @NativeName, flag_emoji = @FlagEmoji,
                is_enabled = @IsEnabled, is_default = @IsDefault,
                requires_human_approval = @RequiresHumanApproval, display_order = @DisplayOrder
            WHERE code = @Code
            """;
        var rows = await db.ExecuteAsync(new CommandDefinition(sql, language, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<ContentTranslation?> GetContentTranslationAsync(long contentId, string targetLanguage, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            SELECT ct.id AS Id, ct.content_id AS ContentId, ct.source_language AS SourceLanguage,
                   ct.target_language AS TargetLanguage, ct.title AS Title, ct.summary AS Summary,
                   ct.body AS Body, ct.cultural_notes AS CulturalNotes, ct.provider AS Provider,
                   ct.model AS Model, ct.version AS Version, ct.review_status AS ReviewStatus,
                   ct.reviewed_by_id AS ReviewedById, u.display_name AS ReviewedByName,
                   ct.reviewed_at AS ReviewedAt, ct.review_notes AS ReviewNotes,
                   ct.created_at AS CreatedAt, ct.updated_at AS UpdatedAt
            FROM mb_content_translations ct
            LEFT JOIN mb_users u ON u.id = ct.reviewed_by_id
            WHERE ct.content_id = @contentId AND ct.target_language = @targetLanguage
            ORDER BY ct.review_status = 'Approved' DESC, ct.version DESC, ct.id DESC
            LIMIT 1
            """;
        return await db.QuerySingleOrDefaultAsync<ContentTranslation>(new CommandDefinition(sql, new { contentId, targetLanguage }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ContentTranslation>> GetContentTranslationsAsync(long contentId, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            SELECT ct.id AS Id, ct.content_id AS ContentId, ct.source_language AS SourceLanguage,
                   ct.target_language AS TargetLanguage, ct.title AS Title, ct.summary AS Summary,
                   ct.body AS Body, ct.cultural_notes AS CulturalNotes, ct.provider AS Provider,
                   ct.model AS Model, ct.version AS Version, ct.review_status AS ReviewStatus,
                   ct.reviewed_by_id AS ReviewedById, u.display_name AS ReviewedByName,
                   ct.reviewed_at AS ReviewedAt, ct.review_notes AS ReviewNotes,
                   ct.created_at AS CreatedAt, ct.updated_at AS UpdatedAt
            FROM mb_content_translations ct
            LEFT JOIN mb_users u ON u.id = ct.reviewed_by_id
            WHERE ct.content_id = @contentId
            ORDER BY ct.target_language ASC, ct.version DESC
            """;
        return (await db.QueryAsync<ContentTranslation>(new CommandDefinition(sql, new { contentId }, cancellationToken: ct))).AsList();
    }

    public async Task<long> UpsertContentTranslationAsync(ContentTranslation translation, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var existingVersion = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT MAX(version) FROM mb_content_translations WHERE content_id = @ContentId AND target_language = @TargetLanguage",
            new { translation.ContentId, translation.TargetLanguage }, cancellationToken: ct)) ?? 0;

        translation.Version = existingVersion + 1;

        var sql = """
            INSERT INTO mb_content_translations
                (content_id, source_language, target_language, title, summary, body,
                 cultural_notes, provider, model, version, review_status,
                 reviewed_by_id, reviewed_at, review_notes)
            VALUES
                (@ContentId, @SourceLanguage, @TargetLanguage, @Title, @Summary, @Body,
                 @CulturalNotes, @Provider, @Model, @Version, @ReviewStatus,
                 @ReviewedById, @ReviewedAt, @ReviewNotes);
            SELECT LAST_INSERT_ID();
            """;

        return await db.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            translation.ContentId,
            translation.SourceLanguage,
            translation.TargetLanguage,
            translation.Title,
            translation.Summary,
            translation.Body,
            translation.CulturalNotes,
            translation.Provider,
            translation.Model,
            translation.Version,
            ReviewStatus = translation.ReviewStatus.ToString(),
            translation.ReviewedById,
            translation.ReviewedAt,
            translation.ReviewNotes
        }, cancellationToken: ct));
    }

    public async Task<bool> SetContentTranslationStatusAsync(long id, TranslationReviewStatus status, long? reviewerId, string? notes, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            UPDATE mb_content_translations
            SET review_status = @status,
                reviewed_by_id = @reviewerId,
                reviewed_at = UTC_TIMESTAMP(6),
                review_notes = @notes
            WHERE id = @id
            """;
        var rows = await db.ExecuteAsync(new CommandDefinition(sql, new
        {
            id,
            status = status.ToString(),
            reviewerId,
            notes
        }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<PlaceTranslation?> GetPlaceTranslationAsync(long placeId, string targetLanguage, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            SELECT pt.id AS Id, pt.place_id AS PlaceId, pt.source_language AS SourceLanguage,
                   pt.target_language AS TargetLanguage, pt.name AS Name,
                   pt.editorial_note AS EditorialNote, pt.cultural_notes AS CulturalNotes,
                   pt.provider AS Provider, pt.model AS Model, pt.version AS Version,
                   pt.review_status AS ReviewStatus, pt.reviewed_by_id AS ReviewedById,
                   u.display_name AS ReviewedByName, pt.reviewed_at AS ReviewedAt,
                   pt.review_notes AS ReviewNotes, pt.created_at AS CreatedAt, pt.updated_at AS UpdatedAt
            FROM mb_place_translations pt
            LEFT JOIN mb_users u ON u.id = pt.reviewed_by_id
            WHERE pt.place_id = @placeId AND pt.target_language = @targetLanguage
            ORDER BY pt.review_status = 'Approved' DESC, pt.version DESC, pt.id DESC
            LIMIT 1
            """;
        return await db.QuerySingleOrDefaultAsync<PlaceTranslation>(new CommandDefinition(sql, new { placeId, targetLanguage }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<PlaceTranslation>> GetPlaceTranslationsAsync(long placeId, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            SELECT pt.id AS Id, pt.place_id AS PlaceId, pt.source_language AS SourceLanguage,
                   pt.target_language AS TargetLanguage, pt.name AS Name,
                   pt.editorial_note AS EditorialNote, pt.cultural_notes AS CulturalNotes,
                   pt.provider AS Provider, pt.model AS Model, pt.version AS Version,
                   pt.review_status AS ReviewStatus, pt.reviewed_by_id AS ReviewedById,
                   u.display_name AS ReviewedByName, pt.reviewed_at AS ReviewedAt,
                   pt.review_notes AS ReviewNotes, pt.created_at AS CreatedAt, pt.updated_at AS UpdatedAt
            FROM mb_place_translations pt
            LEFT JOIN mb_users u ON u.id = pt.reviewed_by_id
            WHERE pt.place_id = @placeId
            ORDER BY pt.target_language ASC, pt.version DESC
            """;
        return (await db.QueryAsync<PlaceTranslation>(new CommandDefinition(sql, new { placeId }, cancellationToken: ct))).AsList();
    }

    public async Task<long> UpsertPlaceTranslationAsync(PlaceTranslation translation, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var existingVersion = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT MAX(version) FROM mb_place_translations WHERE place_id = @PlaceId AND target_language = @TargetLanguage",
            new { translation.PlaceId, translation.TargetLanguage }, cancellationToken: ct)) ?? 0;

        translation.Version = existingVersion + 1;

        var sql = """
            INSERT INTO mb_place_translations
                (place_id, source_language, target_language, name, editorial_note,
                 cultural_notes, provider, model, version, review_status,
                 reviewed_by_id, reviewed_at, review_notes)
            VALUES
                (@PlaceId, @SourceLanguage, @TargetLanguage, @Name, @EditorialNote,
                 @CulturalNotes, @Provider, @Model, @Version, @ReviewStatus,
                 @ReviewedById, @ReviewedAt, @ReviewNotes);
            SELECT LAST_INSERT_ID();
            """;

        return await db.ExecuteScalarAsync<long>(new CommandDefinition(sql, new
        {
            translation.PlaceId,
            translation.SourceLanguage,
            translation.TargetLanguage,
            translation.Name,
            translation.EditorialNote,
            translation.CulturalNotes,
            translation.Provider,
            translation.Model,
            translation.Version,
            ReviewStatus = translation.ReviewStatus.ToString(),
            translation.ReviewedById,
            translation.ReviewedAt,
            translation.ReviewNotes
        }, cancellationToken: ct));
    }

    public async Task<bool> SetPlaceTranslationStatusAsync(long id, TranslationReviewStatus status, long? reviewerId, string? notes, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            UPDATE mb_place_translations
            SET review_status = @status,
                reviewed_by_id = @reviewerId,
                reviewed_at = UTC_TIMESTAMP(6),
                review_notes = @notes
            WHERE id = @id
            """;
        var rows = await db.ExecuteAsync(new CommandDefinition(sql, new
        {
            id,
            status = status.ToString(),
            reviewerId,
            notes
        }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<IReadOnlyList<CulturalGlossaryItem>> GetCulturalGlossaryAsync(CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            SELECT id AS Id, term AS Term, category AS Category,
                   explanation_pt AS ExplanationPt, explanation_en AS ExplanationEn,
                   explanation_fr AS ExplanationFr, explanation_es AS ExplanationEs,
                   explanation_ru AS ExplanationRu, explanation_zh AS ExplanationZh,
                   explanation_ja AS ExplanationJa, created_at AS CreatedAt
            FROM mb_cultural_glossary
            ORDER BY term ASC
            """;
        return (await db.QueryAsync<CulturalGlossaryItem>(new CommandDefinition(sql, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<ContentTranslation>> PendingContentTranslationsAsync(int take, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            SELECT ct.id AS Id, ct.content_id AS ContentId, ct.source_language AS SourceLanguage,
                   ct.target_language AS TargetLanguage, ct.title AS Title, ct.summary AS Summary,
                   ct.body AS Body, ct.cultural_notes AS CulturalNotes, ct.provider AS Provider,
                   ct.model AS Model, ct.version AS Version, ct.review_status AS ReviewStatus,
                   ct.reviewed_by_id AS ReviewedById, ct.reviewed_at AS ReviewedAt,
                   ct.review_notes AS ReviewNotes, ct.created_at AS CreatedAt, ct.updated_at AS UpdatedAt
            FROM mb_content_translations ct
            WHERE ct.review_status = 'Pending'
            ORDER BY ct.created_at DESC
            LIMIT @take
            """;
        return (await db.QueryAsync<ContentTranslation>(new CommandDefinition(sql, new { take }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<PlaceTranslation>> PendingPlaceTranslationsAsync(int take, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            SELECT pt.id AS Id, pt.place_id AS PlaceId, pt.source_language AS SourceLanguage,
                   pt.target_language AS TargetLanguage, pt.name AS Name,
                   pt.editorial_note AS EditorialNote, pt.cultural_notes AS CulturalNotes,
                   pt.provider AS Provider, pt.model AS Model, pt.version AS Version,
                   pt.review_status AS ReviewStatus, pt.reviewed_by_id AS ReviewedById,
                   pt.reviewed_at AS ReviewedAt, pt.review_notes AS ReviewNotes,
                   pt.created_at AS CreatedAt, pt.updated_at AS UpdatedAt
            FROM mb_place_translations pt
            WHERE pt.review_status = 'Pending'
            ORDER BY pt.created_at DESC
            LIMIT @take
            """;
        return (await db.QueryAsync<PlaceTranslation>(new CommandDefinition(sql, new { take }, cancellationToken: ct))).AsList();
    }

    public async Task<TranslationStats> GetStatsAsync(CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var totalContent = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_content_items", cancellationToken: ct));
        var totalPlaces = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_places WHERE is_published = 1", cancellationToken: ct));
        var totalTranslations = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT (SELECT COUNT(*) FROM mb_content_translations) + (SELECT COUNT(*) FROM mb_place_translations)", cancellationToken: ct));
        var pendingReviews = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT (SELECT COUNT(*) FROM mb_content_translations WHERE review_status = 'Pending') + (SELECT COUNT(*) FROM mb_place_translations WHERE review_status = 'Pending')", cancellationToken: ct));
        var approvedTranslations = await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT (SELECT COUNT(*) FROM mb_content_translations WHERE review_status = 'Approved') + (SELECT COUNT(*) FROM mb_place_translations WHERE review_status = 'Approved')", cancellationToken: ct));

        var langCounts = (await db.QueryAsync<(string Lang, int Count)>(new CommandDefinition("""
            SELECT target_language AS Lang, COUNT(*) AS Count
            FROM (
                SELECT target_language FROM mb_content_translations
                UNION ALL
                SELECT target_language FROM mb_place_translations
            ) all_t
            GROUP BY target_language
            """, cancellationToken: ct))).ToDictionary(x => x.Lang, x => x.Count, StringComparer.OrdinalIgnoreCase);

        return new TranslationStats(totalContent, totalPlaces, totalTranslations, pendingReviews, approvedTranslations, langCounts);
    }

    public async Task<IReadOnlyList<ContentItem>> UntranslatedContentAsync(string targetLanguage, int take, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            SELECT id AS Id, title AS Title, summary AS Summary, category AS Category,
                   neighborhood AS Neighborhood, municipality AS Municipality,
                   published_at AS PublishedAt, status AS Status
            FROM mb_content_items c
            WHERE c.status IN ('Published','Draft','InReview')
              AND NOT EXISTS (
                  SELECT 1 FROM mb_content_translations ct
                  WHERE ct.content_id = c.id
                    AND ct.target_language = @targetLanguage
                    AND ct.review_status IN ('Approved','Pending')
              )
            ORDER BY c.published_at DESC
            LIMIT @take
            """;
        return (await db.QueryAsync<ContentItem>(new CommandDefinition(sql, new { targetLanguage, take }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<Place>> UntranslatedPlacesAsync(string targetLanguage, int take, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        var sql = """
            SELECT id AS Id, name AS Name, primary_type AS PrimaryType,
                   editorial_note AS EditorialNote, neighborhood AS Neighborhood,
                   municipality AS Municipality, is_published AS IsPublished
            FROM mb_places p
            WHERE p.is_published = 1
              AND NOT EXISTS (
                  SELECT 1 FROM mb_place_translations pt
                  WHERE pt.place_id = p.id
                    AND pt.target_language = @targetLanguage
                    AND pt.review_status IN ('Approved','Pending')
              )
            ORDER BY p.name ASC
            LIMIT @take
            """;
        return (await db.QueryAsync<Place>(new CommandDefinition(sql, new { targetLanguage, take }, cancellationToken: ct))).AsList();
    }
}
