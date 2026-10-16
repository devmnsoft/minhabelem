using System.Text.Json;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Infrastructure.Discovery;

public abstract class JsonSourceConnector(ConnectorHttp http, FeedProtocol protocol, string? host, bool credentialRequired) : ISourceConnector
{
    public bool Supports(FeedProtocol candidate) => candidate == protocol || protocol == FeedProtocol.Custom && candidate == FeedProtocol.Json;
    public string? ConfigurationIssue(SourceContext context) => http.CredentialIssue(context) ?? (credentialRequired && !http.HasCredential(context.Profile) ? "Aguardando credencial e permissões da API oficial." :
        host is not null && new Uri(context.Source.FeedUrl).Host != host ? "Cadastre o endpoint HTTPS da API oficial, sem credenciais na URL." : null);
    public async Task<ConnectorResult> ReadAsync(SourceContext context, CancellationToken ct)
    {
        var endpoint = context.Source.FeedUrl;
        var existing = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(endpoint).Query);
        var parameters = new Dictionary<string,string?>();
        if (protocol == FeedProtocol.Instagram && !existing.ContainsKey("fields")) parameters["fields"] = "id,caption,permalink,timestamp";
        if (protocol == FeedProtocol.Facebook && !existing.ContainsKey("fields")) parameters["fields"] = "id,message,permalink_url,created_time";
        if (protocol == FeedProtocol.TikTok && !existing.ContainsKey("fields")) parameters["fields"] = "id,title,video_description,share_url,create_time";
        if (protocol == FeedProtocol.YouTube)
        {
            if (!existing.ContainsKey("part")) parameters["part"] = "snippet";
            if (!existing.ContainsKey("type")) parameters["type"] = "video";
            if (!existing.ContainsKey("order")) parameters["order"] = "date";
            if (!existing.ContainsKey("maxResults")) parameters["maxResults"] = Math.Clamp(context.Profile.MaxItems,1,20).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (protocol is FeedProtocol.YouTube or FeedProtocol.WebDiscovery && !existing.ContainsKey("q"))
            parameters["q"] = context.Profile.Keywords + " " + context.Profile.Region + " Brasil -Portugal -Lisboa";
        endpoint = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(endpoint, parameters);
        var requestContext = context with { Source = new IngestionSource { FeedUrl = endpoint, ETag = context.Source.ETag, LastModified = context.Source.LastModified } };
        var read = await http.ReadAsync(requestContext, ct, host, protocol == FeedProtocol.TikTok);
        if (read.Body is null) return new(Array.Empty<DiscoveredItem>(), read.ETag, read.Modified);
        using var doc = JsonDocument.Parse(read.Body, new JsonDocumentOptions { MaxDepth = 32 });
        var root = doc.RootElement;
        var list = root.TryGetProperty("items", out var items) ? items : root.TryGetProperty("data", out var data) ? (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("videos", out var videos) ? videos : data) : default;
        if (list.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("Resposta da API não contém uma lista reconhecida.");
        var result = new List<DiscoveredItem>();
        foreach (var row in list.EnumerateArray().Take(context.Profile.MaxItems))
        {
            var snippet = row.TryGetProperty("snippet", out var sn) && sn.ValueKind == JsonValueKind.Object ? sn : row;
            var title = Text(snippet, "title") ?? Text(row, "caption") ?? Text(row, "message") ?? Text(row, "video_description");
            var link = Text(row, "url") ?? Text(row, "link") ?? Text(row, "permalink") ?? Text(row, "permalink_url") ?? Text(row, "share_url");
            if (protocol == FeedProtocol.YouTube && row.TryGetProperty("id", out var identifier) && identifier.TryGetProperty("videoId", out var videoId)) link = "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(videoId.GetString()!);
            if (title is null || link is null) continue;
            var summary = Text(snippet, "description") ?? Text(row, "summary") ?? Text(row, "snippet") ?? title;
            var dateText = Text(snippet, "publishedAt") ?? Text(row, "timestamp") ?? Text(row, "created_time");
            DateTime? posted = DateTimeOffset.TryParse(dateText, out var date) ? date.UtcDateTime : null;
            if (row.TryGetProperty("create_time", out var epoch) && epoch.TryGetInt64(out var seconds)) posted = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
            result.Add(new(HtmlText.Clean(title, 300), HtmlText.ToPlainText(summary, 1500), link, Text(snippet, "channelTitle") ?? Text(row, "author"), posted, row.TryGetProperty("event", out var eventData) ? StructuredEvents.Evidence(eventData, link) : null));
        }
        string? checkpoint = null;
        if (root.TryGetProperty("data", out var cursorData) && cursorData.ValueKind == JsonValueKind.Object && cursorData.TryGetProperty("cursor", out var next)) checkpoint = next.ToString();
        return new(result, read.ETag, read.Modified, checkpoint);
    }
    private static string? Text(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
public sealed class InstagramConnector(ConnectorHttp http) : JsonSourceConnector(http, FeedProtocol.Instagram, "graph.instagram.com", true), IInstagramConnector;
public sealed class FacebookConnector(ConnectorHttp http) : JsonSourceConnector(http, FeedProtocol.Facebook, "graph.facebook.com", true), IFacebookConnector;
public sealed class TikTokConnector(ConnectorHttp http) : JsonSourceConnector(http, FeedProtocol.TikTok, "open.tiktokapis.com", true), ITikTokConnector;
public sealed class YouTubeConnector(ConnectorHttp http) : JsonSourceConnector(http, FeedProtocol.YouTube, "www.googleapis.com", true), IYouTubeConnector;
public sealed class WebDiscoveryConnector(ConnectorHttp http) : JsonSourceConnector(http, FeedProtocol.WebDiscovery, "customsearch.googleapis.com", true), IWebDiscoveryConnector;
public sealed class GovernmentDataConnector(ConnectorHttp http) : JsonSourceConnector(http, FeedProtocol.GovernmentApi, null, false), IGovernmentDataConnector;
public sealed class CustomJsonConnector(ConnectorHttp http) : JsonSourceConnector(http, FeedProtocol.Custom, null, false);
