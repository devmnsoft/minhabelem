using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Infrastructure.Discovery;

public sealed partial class PublicRssDiscoveryService(IHttpClientFactory httpClientFactory) : IPublicRssDiscoveryService
{
    private static readonly XmlReaderSettings SafeXmlSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = 10_000_000
    };

    [GeneratedRegex(@"<link\b[^>]*(?:rel\s*=\s*['""]alternate['""][^>]*type\s*=\s*['""]application\/(?:rss|atom)\+xml['""]|type\s*=\s*['""]application\/(?:rss|atom)\+xml['""][^>]*rel\s*=\s*['""]alternate['""])[^>]*href\s*=\s*['""]([^'""]+)['""][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex FeedAlternateLinkRegex();

    public async Task<IReadOnlyList<DiscoveredItem>> DiscoverFeedAsync(string feedUrlOrHtml, CancellationToken ct)
    {
        var items = new List<DiscoveredItem>();

        var valid = SafeUrl.Validate(feedUrlOrHtml);
        if (!valid.IsValid || valid.Uri is null) return items;

        var client = httpClientFactory.CreateClient("feed");
        using var response = await client.GetAsync(valid.Uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) return items;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = XmlReader.Create(stream, SafeXmlSettings);

        try
        {
            var feed = SyndicationFeed.Load(reader);
            if (feed is null) return items;

            foreach (var item in feed.Items)
            {
                var title = HtmlText.Clean(item.Title?.Text, 300);
                if (string.IsNullOrWhiteSpace(title)) continue;

                var summary = HtmlText.ToPlainText(item.Summary?.Text ?? (item.Content as TextSyndicationContent)?.Text, 1500);
                var link = item.Links.FirstOrDefault(l => l.RelationshipType is null or "alternate")?.Uri?.ToString()
                    ?? item.Id;

                if (string.IsNullOrWhiteSpace(link) || !SafeUrl.Validate(link).IsValid) continue;

                var author = item.Authors.FirstOrDefault()?.Name ?? item.Authors.FirstOrDefault()?.Email;
                var published = item.PublishDate != default ? item.PublishDate.UtcDateTime : item.LastUpdatedTime != default ? item.LastUpdatedTime.UtcDateTime : (DateTime?)null;

                var enclosure = item.Links.FirstOrDefault(l => l.RelationshipType == "enclosure" && l.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)?.Uri?.ToString();

                items.Add(new DiscoveredItem(
                    Title: title,
                    Summary: summary,
                    Url: link,
                    Author: HtmlText.Clean(author, 180),
                    PostedAt: published,
                    ExternalId: item.Id
                )
                {
                    MediaUrl = enclosure
                });
            }
        }
        catch (Exception)
        {
            // Retorna vazio caso o feed esteja corrompido, sem quebrar o crawler
        }

        return items;
    }

    public Task<IReadOnlyList<string>> DiscoverFeedLinksInHtmlAsync(string html, Uri baseUri, CancellationToken ct)
    {
        var discovered = new List<string>();
        if (string.IsNullOrWhiteSpace(html)) return Task.FromResult<IReadOnlyList<string>>(discovered);

        foreach (Match match in FeedAlternateLinkRegex().Matches(html))
        {
            var href = match.Groups[1].Value.Trim();
            if (Uri.TryCreate(baseUri, href, out var resolvedUri) && SafeUrl.Validate(resolvedUri.ToString()).IsValid)
            {
                discovered.Add(resolvedUri.ToString());
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(discovered);
    }
}
