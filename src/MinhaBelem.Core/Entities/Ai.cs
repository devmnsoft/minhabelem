using MinhaBelem.Core.Taxonomy;

namespace MinhaBelem.Core.Entities;

public enum AiProvider { Groq, Gemini, DeepSeek }

public enum AiTask
{
    Classify,
    Locality,
    TitleAndSummary,
    DuplicateCheck,
    FreshnessCheck,
    SafetyCheck,
    PublishPackage
}

/// <summary>Registro obrigatório de toda chamada de IA: modelo, versão, custo e resultado.</summary>
public sealed class AiRun
{
    public long Id { get; set; }
    public AiProvider Provider { get; set; }
    public string Model { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public AiTask Task { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public long? TargetId { get; set; }
    public int PromptChars { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public decimal CostUsd { get; set; }
    public int LatencyMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ResponseJson { get; set; }
    public long? TriggeredByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record AiClassification(
    ContentCategory Category,
    string? Subject,
    string? Municipality,
    string? Neighborhood,
    IReadOnlyList<Audience> Audiences,
    bool IsFree,
    bool HasEventDate,
    ModerationFlag Flags,
    IReadOnlyList<string> Notes);

public sealed record AiLocality(string? Municipality, string? Neighborhood, string? Address, double? Latitude, double? Longitude);

public sealed record AiPackage(
    string? Title,
    string? Summary,
    ContentCategory? Category,
    string? Subject,
    string? Municipality,
    string? Neighborhood,
    IReadOnlyList<Audience> Audiences,
    bool? IsFree,
    ModerationFlag Flags,
    bool IsDuplicate,
    long? DuplicateOfId,
    bool LooksOutdated,
    string? RawJson);

public sealed record AiSafetyVerdict(bool IsOffensive, bool IsSensitive, bool IsDenunciation, bool IsCriticalAlert, IReadOnlyList<string> Reasons);

/// <summary>Resultado combinado usado pela central editorial.</summary>
public sealed record AiAnalysisResult(
    AiClassification? Classification,
    AiLocality? Locality,
    AiSafetyVerdict? Safety,
    bool IsDuplicate,
    long? DuplicateOfId,
    double DuplicateScore,
    bool LooksOutdated,
    IReadOnlyList<AiRun> Runs)
{
    public Services.EventEvidence? Event { get; init; }
}
