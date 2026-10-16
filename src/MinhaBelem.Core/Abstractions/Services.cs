using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;

namespace MinhaBelem.Core.Services;

public sealed record AiCompletion(
    string Text,
    int? PromptTokens,
    int? CompletionTokens,
    decimal CostUsd,
    string ModelVersion);

/// <summary>Chamada a um provedor de IA. Implementado na infraestrutura com HttpClient nomeado.</summary>
public interface IAiCompletionClient
{
    AiProvider Provider { get; }
    bool IsConfigured { get; }
    string Model { get; }
    Task<AiCompletion> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct);
}

public sealed record AiAnalysisInput(
    string Title,
    string Summary,
    string? SourceUrl,
    IReadOnlyList<ContentItem> ComparisonItems)
{
    public long? TargetId { get; init; }
    public long? ActorId { get; init; }
}

/// <summary>Serviço editorial de IA: classifica, geolocaliza, resume, detecta duplicidade e sinaliza risco.</summary>
public interface IAiAnalysisService
{
    Task<AiAnalysisResult> AnalyzeAsync(AiAnalysisInput input, CancellationToken ct);
}

public sealed record FeedItem(string Title, string Summary, string? Link, string? Author, DateTime? PublishedAt, string? ImageUrl);

/// <summary>Leitor de RSS/Atom. A implementação real fica na infraestrutura.</summary>
public interface IFeedReader
{
    Task<IReadOnlyList<FeedItem>> ReadAsync(Uri feedUri, CancellationToken ct);
}

public sealed record RobotsDecision(bool Allowed, string? Reason);

public interface IRobotsGate
{
    Task<RobotsDecision> IsAllowedAsync(Uri target, CancellationToken ct);
}

public interface IPlacesClient
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<Place>> SearchAsync(string placeType, double latitude, double longitude, double radiusMeters, int maxResults, CancellationToken ct);
    Task<Place?> GetDetailsAsync(string placeId, CancellationToken ct);
    Task<string?> GetPhotoUriAsync(string? photoName, CancellationToken ct);
}

public interface ICurrentUserAccessor
{
    long? UserId { get; }
    string? UserName { get; }
    string? IpAddress { get; }
    bool HasPermission(string permission);
    IReadOnlyList<string> Permissions { get; }
    bool IsInRole(string role);
}
