using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Text;
namespace MinhaBelem.Infrastructure.Discovery;
public static class StructuredEvents
{
    public static IReadOnlyList<DiscoveredItem> ParseJsonLd(string html, string sourceUrl)
    {
        var result = new List<DiscoveredItem>();
        foreach (Match script in Regex.Matches(html, "<script\\b[^>]*type\\s*=\\s*['\"]application/ld\\+json['\"][^>]*>([\\s\\S]*?)</script\\s*>", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
        {
            try { using var doc = JsonDocument.Parse(script.Groups[1].Value, new JsonDocumentOptions { MaxDepth = 32 }); Traverse(doc.RootElement, sourceUrl, result); }
            catch (JsonException) { /* JSON-LD invalido nao autoriza inventar dados. */ }
        }
        return result;
    }
    private static void Traverse(JsonElement value, string source, List<DiscoveredItem> result)
    {
        if (value.ValueKind == JsonValueKind.Array) { foreach (var child in value.EnumerateArray()) Traverse(child, source, result); return; }
        if (value.ValueKind != JsonValueKind.Object) return;
        if (value.TryGetProperty("@graph", out var graph)) Traverse(graph, source, result);
        var type = value.TryGetProperty("@type", out var t) ? t.ToString() : "";
        if (!type.Contains("Event", StringComparison.OrdinalIgnoreCase)) return;
        var name = String(value, "name");
        if (string.IsNullOrWhiteSpace(name)) return;
        var url = String(value, "url") ?? source;
        result.Add(new(HtmlText.Clean(name, 300), HtmlText.ToPlainText(String(value, "description"), 1500), url, Name(value, "organizer"), null, Evidence(value, url)));
    }
    public static EventEvidence Evidence(JsonElement value, string url)
    {
        var start = Date(String(value, "startDate"));
        var end = Date(String(value, "endDate"));
        var location = Name(value, "location");
        if (value.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.Object && loc.TryGetProperty("address", out var address))
            location += " · " + (address.ValueKind == JsonValueKind.String ? address.GetString() : string.Join(", ", new[] { String(address, "streetAddress"), String(address, "addressLocality"), String(address, "addressRegion") }.Where(x => x is not null)));
        var price = value.TryGetProperty("offers", out var offer) && offer.ValueKind == JsonValueKind.Object && offer.TryGetProperty("price", out var p) ? p.ToString() : null;
        return new(start, end, HtmlText.Clean(location, 500), Name(value, "organizer"), String(value, "eventStatus"), price, price == "0", String(value, "typicalAgeRange"), Name(value, "performer"), url, start is null);
    }
    private static DateTime? Date(string? value) => value is not null && Regex.IsMatch(value, @"(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase) && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.UtcDateTime : null;
    private static string? String(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private static string? Name(JsonElement value, string key) => value.TryGetProperty(key, out var child) ? child.ValueKind == JsonValueKind.String ? child.GetString() : String(child, "name") : null;
}
public sealed class JsonLdConnector(ConnectorHttp http) : ISourceConnector
{
    public bool Supports(Core.Entities.FeedProtocol protocol) => protocol == Core.Entities.FeedProtocol.JsonLd;
    public string? ConfigurationIssue(SourceContext context) => null;
    public async Task<ConnectorResult> ReadAsync(SourceContext context, CancellationToken ct)
    {
        var read = await http.ReadAsync(context, ct);
        return new(read.Body is null ? Array.Empty<DiscoveredItem>() : StructuredEvents.ParseJsonLd(read.Body, context.Source.FeedUrl), read.ETag, read.Modified);
    }
}
