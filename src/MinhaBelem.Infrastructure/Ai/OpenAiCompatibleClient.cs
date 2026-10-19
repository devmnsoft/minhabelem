using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Infrastructure.Networking;

namespace MinhaBelem.Infrastructure.Ai;

public sealed class AiProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal InputCostPerMillion { get; set; }
    public decimal OutputCostPerMillion { get; set; }
    public int TimeoutSeconds { get; set; } = 40;
}

public sealed class AiOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxDailyRequests { get; set; } = 50;
    public string DefaultProvider { get; set; } = nameof(AiProvider.Groq);
    public AiProviderOptions Groq { get; set; } = new();
    public AiProviderOptions Gemini { get; set; } = new();
    public AiProviderOptions DeepSeek { get; set; } = new();

    public AiProviderOptions For(AiProvider provider) => provider switch
    {
        AiProvider.Groq => Groq,
        AiProvider.Gemini => Gemini,
        AiProvider.DeepSeek => DeepSeek,
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };
}

public static class AiEndpoints
{
    public const string Groq = "https://api.groq.com/openai/v1/chat/completions";
    public const string DeepSeek = "https://api.deepseek.com/chat/completions";
    public const string Gemini = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
}

/// <summary>
/// Cliente único para os três provedores: todos expõem a API compatível da OpenAI
/// (/chat/completions), o que permite um só caminho de código com custo e latência registrados.
/// </summary>
public sealed class OpenAiCompatibleClient : IAiCompletionClient
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly AiOptions options;
    private readonly ILogger<OpenAiCompatibleClient> logger;
    private readonly ApiBudget budget;
    public AiProvider Provider { get; }

    public OpenAiCompatibleClient(IHttpClientFactory httpClientFactory, AiOptions options, ILogger<OpenAiCompatibleClient> logger, ApiBudget budget, AiProvider provider = AiProvider.Groq)
    {
        this.httpClientFactory = httpClientFactory;
        this.options = options;
        this.logger = logger;
        this.budget = budget;
        Provider = provider;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ProviderOptions.ApiKey);

    public string Model => ProviderOptions.Model;

    private AiProviderOptions ProviderOptions => options.For(Provider);

    /// <summary>
    /// Disjuntor por provedor: após falhas consecutivas (por exemplo, credencial sem
    /// permissão habilitada), o provedor é ignorado por um período para não gastar
    /// latência a cada requisição. O motor nativo do Curió cobre a resposta.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<AiProvider, (int Failures, DateTime Until)> Breaker = new();

    private bool IsTripped => IsProviderTripped(Provider);

    /// <summary>Indica se o disjuntor do provedor está aberto (provedor temporariamente indisponível).</summary>
    public static bool IsProviderTripped(AiProvider provider) =>
        Breaker.TryGetValue(provider, out var state) && state.Failures >= TripThreshold && state.Until > DateTime.UtcNow;

    private const int TripThreshold = 3;
    private static readonly TimeSpan TripDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);

    private void RegisterFailure()
    {
        Breaker.AddOrUpdate(Provider, _ => (1, DateTime.UtcNow + TripDuration),
            (_, current) => current.Failures == 0 || current.Until < DateTime.UtcNow
                ? (1, DateTime.UtcNow + TripDuration)
                : (current.Failures + 1, DateTime.UtcNow + TripDuration));
    }

    private void RegisterSuccess() => Breaker.TryRemove(Provider, out _);

    public async Task<AiCompletion> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException($"Provedor {Provider} sem credencial configurada.");
        if (IsTripped) throw new InvalidOperationException($"Provedor {Provider} indisponível temporariamente (disjuntor aberto).");
        if (!await budget.ReserveAsync("ai-" + Provider, options.MaxDailyRequests, ct)) throw new InvalidOperationException("Limite diário de IA atingido.");

        var endpoint = Provider switch
        {
            AiProvider.Groq => AiEndpoints.Groq,
            AiProvider.DeepSeek => AiEndpoints.DeepSeek,
            AiProvider.Gemini => AiEndpoints.Gemini,
            _ => throw new ArgumentOutOfRangeException(nameof(Provider))
        };

        var payload = new
        {
            model = ProviderOptions.Model,
            temperature = 0.1,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ProviderOptions.ApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var client = httpClientFactory.CreateClient("ai");
        var stopwatch = Stopwatch.StartNew();
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        stopwatch.Stop();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("{Provider} respondeu {Status}", Provider, (int)response.StatusCode);
            RegisterFailure();
            throw new HttpRequestException($"{Provider} respondeu {(int)response.StatusCode}.");
        }

        RegisterSuccess();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = document.RootElement;
        var choice = root.GetProperty("choices")[0];
        var text = choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

        var promptTokens = TryReadInt(root, "prompt_tokens");
        var completionTokens = TryReadInt(root, "completion_tokens");
        var modelVersion = root.TryGetProperty("model", out var model) ? model.GetString() ?? ProviderOptions.Model : ProviderOptions.Model;
        var cost = CalculateCost(promptTokens, completionTokens);

        return new AiCompletion(text, promptTokens, completionTokens, cost, modelVersion);
    }

    private static int? TryReadInt(JsonElement root, string property)
    {
        if (!root.TryGetProperty("usage", out var usage) || !usage.TryGetProperty(property, out var value)) return null;
        return value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;
    }

    private decimal CalculateCost(int? promptTokens, int? completionTokens)
    {
        if (promptTokens is null || completionTokens is null) return 0m;
        var opt = ProviderOptions;
        return Math.Round(
            (promptTokens.Value / 1_000_000m * opt.InputCostPerMillion) +
            (completionTokens.Value / 1_000_000m * opt.OutputCostPerMillion), 6);
    }
}

/// <summary>Registro de execuções de IA; injeta o log em <c>mb_ai_runs</c>.</summary>
public sealed class AiRunLogger(MinhaBelem.Core.Abstractions.IAiRunRepository runs, ILogger<AiRunLogger> logger)
{
    public async Task<AiRun> RecordAsync(
        IAiCompletionClient client,
        AiTask task,
        string targetType,
        long? targetId,
        string promptChars,
        long? userId,
        Func<AiCompletion, Task<string?>>? capture = null,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var completion = await client.CompleteAsync(AiPrompts.System, promptChars, ct);
            stopwatch.Stop();
            var responseJson = capture is null ? null : await capture(completion);
            if (responseJson is not null)
            {
                try { using var json = JsonDocument.Parse(responseJson); }
                catch (JsonException) { responseJson = JsonSerializer.Serialize(new { raw = responseJson }); }
            }

            var run = new AiRun
            {
                Provider = client.Provider,
                Model = client.Model,
                ModelVersion = completion.ModelVersion,
                Task = task,
                TargetType = targetType,
                TargetId = targetId,
                PromptChars = promptChars.Length,
                PromptTokens = completion.PromptTokens,
                CompletionTokens = completion.CompletionTokens,
                CostUsd = completion.CostUsd,
                LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                Success = true,
                ResponseJson = responseJson,
                TriggeredByUserId = userId
            };
            await runs.SaveAsync(run, ct);
            return run;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogWarning("Falha na tarefa de IA {Task} via {Provider}: {ErrorType}", task, client.Provider, ex.GetType().Name);
            var run = new AiRun
            {
                Provider = client.Provider,
                Model = client.Model,
                ModelVersion = client.Model,
                Task = task,
                TargetType = targetType,
                TargetId = targetId,
                PromptChars = promptChars.Length,
                LatencyMs = (int)stopwatch.ElapsedMilliseconds,
                Success = false,
                Error = ex is HttpRequestException http ? "HTTP " + http.StatusCode : ex.GetType().Name,
                TriggeredByUserId = userId
            };
            await runs.SaveAsync(run, ct);
            return run;
        }
    }
}
