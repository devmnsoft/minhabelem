using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Security;

namespace MinhaBelem.Infrastructure.Feeds;

public sealed partial class RobotsGate(HttpClient client, IMemoryCache cache, ILogger<RobotsGate> logger) : IRobotsGate
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    [GeneratedRegex(@"^\s*(?<path>[^\s#]+)\s*:\s*(?<value>.*)$")]
    private static partial Regex RuleLine();

    [GeneratedRegex(@"^\s*(user-agent|disallow|allow|crawl-delay|sitemap)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex KnownDirective();

    public async Task<RobotsDecision> IsAllowedAsync(Uri target, CancellationToken ct)
    {
        var origin = target.GetLeftPart(UriPartial.Authority);
        var rules = await LoadAsync(origin, ct);

        // "*" é o grupo padrão; grupos específicos têm prioridade sobre ele.
        if (!rules.Any(r => r.Agent == "*")) return new RobotsDecision(true, "Sem regras aplicáveis.");

        var path = target.PathAndQuery;
        var best = rules
            .Where(r => r.Agent == "*" && r.Path.Length > 0)
            .Where(r => path.StartsWith(r.Path, StringComparison.Ordinal))
            .OrderBy(r => r.Path.Length)
            .ThenBy(r => r.Allow ? 1 : 0)
            .LastOrDefault();

        if (best is null) return new RobotsDecision(true, null);
        return best.Allow
            ? new RobotsDecision(true, null)
            : new RobotsDecision(false, $"robots.txt de {origin} proíbe '{path}'.");
    }

    private async Task<IReadOnlyList<RobotsRule>> LoadAsync(string origin, CancellationToken ct)
    {
        if (cache.TryGetValue(origin, out IReadOnlyList<RobotsRule>? cached) && cached is not null) return cached;

        var rules = new List<RobotsRule>();
        try
        {
            using var response = await client.GetAsync(new Uri(origin + "/robots.txt"), ct);
            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                rules.AddRange(Parse(body));
            }
            else if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                logger.LogWarning("robots.txt de {Origin} respondeu {Status}; tratando como bloqueado.", origin, (int)response.StatusCode);
                rules.Add(new RobotsRule("*", "/", Allow: false));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Falha ao obter robots.txt de {Origin}; tratando como bloqueado.", origin);
            rules.Add(new RobotsRule("*", "/", Allow: false));
        }

        cache.Set(origin, (IReadOnlyList<RobotsRule>)rules, CacheTtl);
        return rules;
    }

    internal static IReadOnlyList<RobotsRule> Parse(string content)
    {
        var rules = new List<RobotsRule>();
        var appliesTo = "*";
        var lines = content.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (!KnownDirective().IsMatch(line)) continue;

            var match = RuleLine().Match(line);
            if (!match.Success) continue;
            var key = match.Groups["path"].Value.Trim().ToLowerInvariant();
            var value = match.Groups["value"].Value.Trim();

            switch (key)
            {
                case "user-agent":
                    appliesTo = value;
                    break;
                case "disallow":
                    // "Disallow:" vazio significa "permitir tudo".
                    rules.Add(new RobotsRule(appliesTo, value, Allow: false));
                    break;
                case "allow":
                    rules.Add(new RobotsRule(appliesTo, value, Allow: true));
                    break;
            }
        }
        return rules;
    }

    internal sealed record RobotsRule(string Agent, string Path, bool Allow);
}

public static class SafeUrlExtensions
{
    /// <summary>Valida e devolve a Uri, ou null com o motivo em <paramref name="reason"/>.</summary>
    public static Uri? TryParseSafe(this string? raw, out string? reason)
    {
        var result = SafeUrl.Validate(raw);
        reason = result.Reason;
        return result.Uri;
    }
}