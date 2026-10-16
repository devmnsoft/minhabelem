using System.Text.Json;
using Microsoft.Extensions.Logging;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;

namespace MinhaBelem.Infrastructure.Ai;

public sealed class ContentTranslationService : IContentTranslationService
{
    private readonly IAiClientResolver resolver;
    private readonly AiOptions options;
    private readonly AiRunLogger runLogger;
    private readonly ITranslationRepository translationRepository;
    private readonly ILogger<ContentTranslationService> logger;

    public ContentTranslationService(
        IAiClientResolver resolver,
        AiOptions options,
        AiRunLogger runLogger,
        ITranslationRepository translationRepository,
        ILogger<ContentTranslationService> logger)
    {
        this.resolver = resolver;
        this.options = options;
        this.runLogger = runLogger;
        this.translationRepository = translationRepository;
        this.logger = logger;
    }

    private const string TranslationSystemPrompt = """
        You are an elite multilingual cultural and tourist translator for MinhaBelém, an official portal dedicated to Belém do Pará, Brazil (Amazon region).

        CRITICAL REGIONAL AND CULTURAL INTEGRITY RULES:
        1. Always preserve regional proper names, cuisine, cultural traditions, and geography without literal translation:
           Examples: "Ver-o-Peso", "Círio de Nazaré", "tacacá", "maniçoba", "carimbó", "açaí", "tucupi", "jambu", "cuia", "Combu", "Mosqueiro", "Icoaraci", "Theatro da Paz", "Mangal das Garças", "Feliz Lusitânia", "Forte do Presépio".
        2. In the "cultural_notes" field, provide short, elegant explanations of any Amazonian cultural terms present in the text to assist international visitors.
        3. Translate all narrative and functional content fluently, idiomatically, and accurately into the requested target language.
        4. For Russian (ru), use modern Cyrillic. For Simplified Chinese (zh-Hans), use standard simplified characters. For Japanese (ja), use natural Japanese with appropriate kanji and kana.
        5. You MUST return ONLY valid JSON matching this schema:
        {
          "title": "translated title",
          "summary": "translated summary",
          "body": "translated body",
          "cultural_notes": "cultural context notes for visitors",
          "detected_source_language": "pt-BR"
        }
        """;

    public async Task<ContentTranslationResult> TranslateContentAsync(ContentItem content, string targetLanguage, long? actorId, CancellationToken ct)
    {
        if (content is null) throw new ArgumentNullException(nameof(content));
        if (string.IsNullOrWhiteSpace(targetLanguage)) throw new ArgumentException("Target language required", nameof(targetLanguage));

        var candidates = resolver.Candidates(options);
        if (candidates.Count == 0)
        {
            return new ContentTranslationResult(
                Success: false,
                Title: content.Title,
                Summary: content.Summary,
                Body: content.Body,
                CulturalNotes: null,
                DetectedSourceLanguage: "pt-BR",
                Provider: "None",
                Model: "None",
                CostUsd: 0,
                Error: "Nenhum provedor de IA configurado ou habilitado.");
        }

        var promptPayload = new
        {
            target_language = targetLanguage,
            title = content.Title,
            summary = content.Summary,
            body = content.Body ?? string.Empty,
            category = content.Category.ToString(),
            neighborhood = content.Neighborhood ?? string.Empty
        };
        var promptText = JsonSerializer.Serialize(promptPayload);

        Exception? lastException = null;
        foreach (var client in candidates)
        {
            try
            {
                var run = await runLogger.RecordAsync(
                    client,
                    AiTask.Translate,
                    targetType: nameof(ContentItem),
                    targetId: content.Id,
                    promptChars: promptText,
                    userId: actorId,
                    capture: completion => Task.FromResult<string?>(completion.Text),
                    ct: ct);

                if (!run.Success || string.IsNullOrWhiteSpace(run.ResponseJson))
                {
                    lastException = new InvalidOperationException(run.Error ?? "Resposta vazia da IA.");
                    continue;
                }

                var parsed = ParseResponse(run.ResponseJson);
                var langConfig = await translationRepository.GetLanguageAsync(targetLanguage, ct);
                var reviewStatus = (langConfig?.RequiresHumanApproval ?? true)
                    ? TranslationReviewStatus.Pending
                    : TranslationReviewStatus.Approved;

                var translation = new ContentTranslation
                {
                    ContentId = content.Id,
                    SourceLanguage = parsed.DetectedSourceLanguage ?? "pt-BR",
                    TargetLanguage = targetLanguage,
                    Title = parsed.Title ?? content.Title,
                    Summary = parsed.Summary ?? content.Summary,
                    Body = parsed.Body ?? content.Body,
                    CulturalNotes = parsed.CulturalNotes,
                    Provider = client.Provider.ToString(),
                    Model = client.Model,
                    ReviewStatus = reviewStatus,
                    ReviewedById = reviewStatus == TranslationReviewStatus.Approved ? actorId : null,
                    ReviewedAt = reviewStatus == TranslationReviewStatus.Approved ? DateTime.UtcNow : null,
                    ReviewNotes = reviewStatus == TranslationReviewStatus.Approved ? "Aprovação automática por política de idioma." : null
                };

                var id = await translationRepository.UpsertContentTranslationAsync(translation, ct);
                translation.Id = id;

                return new ContentTranslationResult(
                    Success: true,
                    Title: translation.Title,
                    Summary: translation.Summary,
                    Body: translation.Body,
                    CulturalNotes: translation.CulturalNotes,
                    DetectedSourceLanguage: translation.SourceLanguage,
                    Provider: translation.Provider,
                    Model: translation.Model,
                    CostUsd: run.CostUsd,
                    Error: null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                logger.LogWarning(ex, "Falha na tradução via {Provider}; tentando próximo candidato.", client.Provider);
            }
        }

        return new ContentTranslationResult(
            Success: false,
            Title: content.Title,
            Summary: content.Summary,
            Body: content.Body,
            CulturalNotes: null,
            DetectedSourceLanguage: "pt-BR",
            Provider: "None",
            Model: "None",
            CostUsd: 0,
            Error: lastException?.Message ?? "Todos os provedores de tradução falharam.");
    }

    public async Task<PlaceTranslationResult> TranslatePlaceAsync(Place place, string targetLanguage, long? actorId, CancellationToken ct)
    {
        if (place is null) throw new ArgumentNullException(nameof(place));
        if (string.IsNullOrWhiteSpace(targetLanguage)) throw new ArgumentException("Target language required", nameof(targetLanguage));

        var candidates = resolver.Candidates(options);
        if (candidates.Count == 0)
        {
            return new PlaceTranslationResult(
                Success: false,
                Name: place.Name,
                EditorialNote: place.EditorialNote,
                CulturalNotes: null,
                DetectedSourceLanguage: "pt-BR",
                Provider: "None",
                Model: "None",
                CostUsd: 0,
                Error: "Nenhum provedor de IA configurado ou habilitado.");
        }

        var promptPayload = new
        {
            target_language = targetLanguage,
            place_name = place.Name,
            editorial_note = place.EditorialNote ?? string.Empty,
            primary_type = place.PrimaryType ?? string.Empty,
            neighborhood = place.Neighborhood ?? string.Empty
        };
        var promptText = JsonSerializer.Serialize(promptPayload);

        Exception? lastException = null;
        foreach (var client in candidates)
        {
            try
            {
                var run = await runLogger.RecordAsync(
                    client,
                    AiTask.Translate,
                    targetType: nameof(Place),
                    targetId: place.Id,
                    promptChars: promptText,
                    userId: actorId,
                    capture: completion => Task.FromResult<string?>(completion.Text),
                    ct: ct);

                if (!run.Success || string.IsNullOrWhiteSpace(run.ResponseJson))
                {
                    lastException = new InvalidOperationException(run.Error ?? "Resposta vazia da IA.");
                    continue;
                }

                var parsed = ParseResponse(run.ResponseJson);
                var langConfig = await translationRepository.GetLanguageAsync(targetLanguage, ct);
                var reviewStatus = (langConfig?.RequiresHumanApproval ?? true)
                    ? TranslationReviewStatus.Pending
                    : TranslationReviewStatus.Approved;

                var translation = new PlaceTranslation
                {
                    PlaceId = place.Id,
                    SourceLanguage = parsed.DetectedSourceLanguage ?? "pt-BR",
                    TargetLanguage = targetLanguage,
                    Name = parsed.Title ?? place.Name,
                    EditorialNote = parsed.Summary ?? parsed.Body ?? place.EditorialNote,
                    CulturalNotes = parsed.CulturalNotes,
                    Provider = client.Provider.ToString(),
                    Model = client.Model,
                    ReviewStatus = reviewStatus,
                    ReviewedById = reviewStatus == TranslationReviewStatus.Approved ? actorId : null,
                    ReviewedAt = reviewStatus == TranslationReviewStatus.Approved ? DateTime.UtcNow : null,
                    ReviewNotes = reviewStatus == TranslationReviewStatus.Approved ? "Aprovação automática por política de idioma." : null
                };

                var id = await translationRepository.UpsertPlaceTranslationAsync(translation, ct);
                translation.Id = id;

                return new PlaceTranslationResult(
                    Success: true,
                    Name: translation.Name,
                    EditorialNote: translation.EditorialNote,
                    CulturalNotes: translation.CulturalNotes,
                    DetectedSourceLanguage: translation.SourceLanguage,
                    Provider: translation.Provider,
                    Model: translation.Model,
                    CostUsd: run.CostUsd,
                    Error: null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                logger.LogWarning(ex, "Falha na tradução de lugar via {Provider}; tentando próximo candidato.", client.Provider);
            }
        }

        return new PlaceTranslationResult(
            Success: false,
            Name: place.Name,
            EditorialNote: place.EditorialNote,
            CulturalNotes: null,
            DetectedSourceLanguage: "pt-BR",
            Provider: "None",
            Model: "None",
            CostUsd: 0,
            Error: lastException?.Message ?? "Todos os provedores de tradução falharam.");
    }

    public Task<string> DetectLanguageAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return Task.FromResult("pt-BR");
        var lower = text.ToLowerInvariant();

        // Heurísticas rápidas para alfabetos e vocabulário chave
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"[\u4e00-\u9fa5]")) return Task.FromResult("zh-Hans");
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"[\u3040-\u309f\u30a0-\u30ff]")) return Task.FromResult("ja");
        if (System.Text.RegularExpressions.Regex.IsMatch(text, @"[\u0400-\u04ff]")) return Task.FromResult("ru");

        if (lower.Contains("the ") || lower.Contains("where ") || lower.Contains("museum") || lower.Contains("what ") || lower.Contains("weekend"))
            return Task.FromResult("en");
        if (lower.Contains("où ") || lower.Contains("que ") || lower.Contains("événement") || lower.Contains("musée") || lower.Contains("gratuit"))
            return Task.FromResult("fr");
        if (lower.Contains("dónde ") || lower.Contains("museos") || lower.Contains("gratis") || lower.Contains("qué hacer"))
            return Task.FromResult("es");

        return Task.FromResult("pt-BR");
    }

    private static ParsedTranslation ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("raw", out var raw))
            {
                return ParseResponse(raw.GetString() ?? "{}");
            }

            return new ParsedTranslation(
                Title: root.TryGetProperty("title", out var t) ? t.GetString() : null,
                Summary: root.TryGetProperty("summary", out var s) ? s.GetString() : null,
                Body: root.TryGetProperty("body", out var b) ? b.GetString() : null,
                CulturalNotes: root.TryGetProperty("cultural_notes", out var c) ? c.GetString() : null,
                DetectedSourceLanguage: root.TryGetProperty("detected_source_language", out var d) ? d.GetString() : "pt-BR"
            );
        }
        catch (JsonException)
        {
            return new ParsedTranslation(null, null, null, null, "pt-BR");
        }
    }

    private sealed record ParsedTranslation(
        string? Title,
        string? Summary,
        string? Body,
        string? CulturalNotes,
        string? DetectedSourceLanguage);
}
