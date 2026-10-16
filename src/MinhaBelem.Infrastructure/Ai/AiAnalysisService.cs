using System.Text.Json;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Infrastructure.Ai;

/// <summary>
/// Serviço editorial de IA. Executa no máximo duas chamadas por análise (triagem completa e
/// checagem de duplicidade), registra cada uma em <c>mb_ai_runs</c> e devolve resultado parcial
/// quando o provedor falha — a edição manual sempre permanece disponível.
/// </summary>
public sealed class AiAnalysisService(
    AiRunLogger runLogger,
    IAiClientResolver resolver,
    AiOptions options,
    ILogger<AiAnalysisService> logger) : IAiAnalysisService
{
    public async Task<AiAnalysisResult> AnalyzeAsync(AiAnalysisInput input, CancellationToken ct)
    {
        var runs = new List<AiRun>();
        AiClassification? classification = null;
        AiLocality? locality = null;
        AiSafetyVerdict? safety = null;
        Core.Services.EventEvidence? eventEvidence = null;

        var client = resolver.Resolve(options);
        if (client is null || !client.IsConfigured)
        {
            logger.LogInformation("Nenhum provedor de IA configurado; análise ignorada.");
            return new AiAnalysisResult(null, null, null, false, null, 0, false, runs);
        }

        var analysisPrompt = AiPrompts.BuildAnalysisPrompt(
            input.Title, input.Summary, input.SourceUrl,
            TaxonomyInfo.All.Select(c => c.Slug).ToArray(),
            TaxonomyInfo.Subjects.Select(s => s.Label).ToArray(),
            BelemGeography.BelemNeighborhoods,
            BelemGeography.Municipalities.Select(m => m.Name).ToArray(),
            Enum.GetNames<Audience>());

        var raw = new Dictionary<string, string?>();
        var analysisRun = await runLogger.RecordAsync(client, AiTask.PublishPackage, "content", input.TargetId,
            analysisPrompt, input.ActorId, async completion =>
            {
                raw["analysis"] = completion.Text;
                return completion.Text;
            }, ct);
        runs.Add(analysisRun);

        if (analysisRun.Success && raw.TryGetValue("analysis", out var analysisText))
        {
            ParseAnalysis(analysisText ?? string.Empty, input, out classification, out locality, out safety);
            using var eventDoc = TryParseObject(analysisText);
            if (eventDoc?.RootElement.TryGetProperty("evento", out var eventValue) == true && eventValue.ValueKind == JsonValueKind.Object)
                eventEvidence = Discovery.StructuredEvents.Evidence(eventValue, input.SourceUrl ?? "");
        }

        var isDuplicate = false;
        long? duplicateOfId = null;
        var duplicateScore = 0d;

        if (input.ComparisonItems.Count > 0)
        {
            var duplicatePrompt = AiPrompts.BuildDuplicatePrompt(
                input.Title, input.Summary,
                input.ComparisonItems.Select(c => (c.Id, c.Title)).ToArray());

            var duplicateRun = await runLogger.RecordAsync(client, AiTask.DuplicateCheck, "content", input.TargetId,
                duplicatePrompt, input.ActorId, completion => Task.FromResult<string?>(completion.Text), ct);
            runs.Add(duplicateRun);

            if (duplicateRun.Success)
            {
                var index = ExtractDuplicate(duplicateRun.ResponseJson, out var score);
                if (index is >= 0 && index < input.ComparisonItems.Count && score >= 0.7)
                {
                    isDuplicate = true;
                    duplicateScore = score;
                    duplicateOfId = input.ComparisonItems[index.Value].Id;
                }
            }
        }

        return new AiAnalysisResult(classification, locality, safety, isDuplicate, duplicateOfId, duplicateScore, classification?.Flags.HasFlag(ModerationFlag.Outdated) == true, runs) { Event = eventEvidence };
    }

    private static int? ExtractDuplicate(string? json, out double score)
    {
        score = 0;
        using var document = TryParseObject(json);
        if (document is null) return null;
        var root = document.RootElement;
        if (!root.TryGetProperty("duplicado", out var flag) || flag.ValueKind is not JsonValueKind.True) return null;
        score = root.TryGetProperty("confianca", out var confidence) && confidence.ValueKind == JsonValueKind.Number
            ? confidence.GetDouble() : 1;
        return root.TryGetProperty("indice_duplicado", out var index) && index.ValueKind == JsonValueKind.Number
            ? index.GetInt32() - 1
            : null;
    }

    private void ParseAnalysis(string text, AiAnalysisInput input, out AiClassification? classification, out AiLocality? locality, out AiSafetyVerdict? safety)
    {
        classification = null;
        locality = null;
        safety = null;

        using var document = TryParseObject(text);
        if (document is null) return;
        var root = document.RootElement;

        var category = TaxonomyInfo.Resolve(ReadString(root, "categoria")) ?? ContentCategory.Cidades;
        var subjectLabel = ReadString(root, "assunto");
        var subject = TaxonomyInfo.Subjects.FirstOrDefault(s =>
            string.Equals(s.Label, subjectLabel, StringComparison.OrdinalIgnoreCase))?.Key;

        var neighborhood = BelemGeography.ResolveNeighborhood(ReadString(root, "bairro"));
        var municipalityName = BelemGeography.ResolveMunicipality(ReadString(root, "municipio"))?.Name;

        var audiences = ReadArray(root, "publicos")
            .Select(a => Enum.TryParse<Audience>(a, true, out var parsed) ? parsed : (Audience?)null)
            .Where(a => a is not null).Select(a => a!.Value).Distinct().ToArray();

        var offensive = ReadBool(root, "ofensivo") ?? true;
        var sensitive = ReadBool(root, "sensivel") ?? true;
        var denominator = ReadBool(root, "denuncia") ?? true;
        var critical = ReadBool(root, "alerta_critico") ?? true;
        var outdated = ReadBool(root, "desatualizado") ?? true;

        var flags = ModerationFlag.None;
        if (offensive) flags |= ModerationFlag.Offensive;
        if (sensitive || critical) flags |= ModerationFlag.Sensitive;
        if (denominator) flags |= ModerationFlag.Denunciation;
        if (critical) flags |= ModerationFlag.CriticalAlert;
        if (outdated) flags |= ModerationFlag.Outdated;
        if (category == ContentCategory.Politica) flags |= ModerationFlag.Sensitive;

        var notes = ReadArray(root, "motivos");

        classification = new AiClassification(
            category, subject, municipalityName, neighborhood, audiences,
            ReadBool(root, "gratuito") == true, ReadBool(root, "tem_data") == true,
            flags, notes);

        locality = new AiLocality(municipalityName, neighborhood, ReadString(root, "endereco"), null, null);
        safety = new AiSafetyVerdict(offensive, sensitive, denominator, critical, notes);
    }

    private static JsonDocument? TryParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        var start = json.IndexOf('{');
        var end = json.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        try { return JsonDocument.Parse(json[start..(end + 1)]); }
        catch (JsonException) { return null; }
    }

    private static string? ReadString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? HtmlText.Clean(value.GetString(), 200)
            : null;

    private static bool? ReadBool(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static string[] ReadArray(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        return value.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => HtmlText.Clean(e.GetString(), 60))
            .Where(s => s.Length > 0)
            .ToArray();
    }
}

public interface IAiClientResolver
{
    IAiCompletionClient? Resolve(AiOptions options);
}

public sealed class AiClientResolver : IAiClientResolver
{
    private readonly Networking.ApiBudget budget;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public AiClientResolver(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory, Networking.ApiBudget budget)
    {
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        this.budget = budget;
    }

    public IAiCompletionClient? Resolve(AiOptions options)
    {
        if (!options.Enabled) return null;

        if (Enum.TryParse<AiProvider>(options.DefaultProvider, ignoreCase: true, out var preferred) &&
            options.For(preferred) is { } preferredOptions && !string.IsNullOrWhiteSpace(preferredOptions.ApiKey))
            return Create(preferred, options);

        foreach (var provider in new[] { AiProvider.Groq, AiProvider.Gemini, AiProvider.DeepSeek })
        {
            if (!string.IsNullOrWhiteSpace(options.For(provider).ApiKey))
                return Create(provider, options);
        }
        return null;
    }

    private IAiCompletionClient Create(AiProvider provider, AiOptions options) =>
        new OpenAiCompatibleClient(
            _httpClientFactory, options,
            _loggerFactory.CreateLogger<OpenAiCompatibleClient>(), budget, provider);
}
