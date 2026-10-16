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
        var specific = rules.Where(r => r.Agent != "*" && "MinhaBelemBot".Contains(r.Agent, StringComparison.OrdinalIgnoreCase)).ToArray();
        var selected = specific.Length == 0 ? rules.Where(r => r.Agent == "*") : specific.Where(r => r.Agent.Length == specific.Max(x => x.Agent.Length));

        var path = target.PathAndQuery;
        var best = selected
            .Where(r => r.Path.Length > 0)
            .Where(r => Matches(path, r.Path))
            .OrderBy(r => r.Path.Replace("*", "", StringComparison.Ordinal).TrimEnd('$').Length)
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
            using var response = await client.GetAsync(new Uri(origin + "/robots.txt"), HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode)
            {
                if (response.Content.Headers.ContentLength is > 512000) throw new InvalidOperationException("robots.txt excede o limite.");
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);
                var buffer = new char[512001]; var count = await reader.ReadBlockAsync(buffer.AsMemory(), ct);
                if (count > 512000) throw new InvalidOperationException("robots.txt excede o limite.");
                var body = new string(buffer, 0, count);
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
        var agents = new List<string>();
        var hasRules = false;
        var lines = content.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (!KnownDirective().IsMatch(line)) continue;

            var match = RuleLine().Match(line);
            if (!match.Success) continue;
            var key = match.Groups["path"].Value.Trim().ToLowerInvariant();
            var value = match.Groups["value"].Value.Split('#')[0].Trim();

            switch (key)
            {
                case "user-agent":
                    if (hasRules) { agents.Clear(); hasRules = false; }
                    if (value.Length > 0) agents.Add(value);
                    break;
                case "disallow":
                    // "Disallow:" vazio significa "permitir tudo".
                    hasRules = true;
                    rules.AddRange(agents.Select(agent => new RobotsRule(agent, value, Allow: false)));
                    break;
                case "allow":
                    hasRules = true;
                    rules.AddRange(agents.Select(agent => new RobotsRule(agent, value, Allow: true)));
                    break;
            }
        }
        return rules;
    }

    internal sealed record RobotsRule(string Agent, string Path, bool Allow);
    private static bool Matches(string path, string rule)
    {
        var end = rule.EndsWith('$');
        var pattern = Regex.Escape(end ? rule[..^1] : rule).Replace("\\*", ".*", StringComparison.Ordinal);
        return Regex.IsMatch(path, "^" + pattern + (end ? "$" : ""), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }
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
