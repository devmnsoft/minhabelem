using System.ServiceModel.Syndication;
using System.Xml;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Infrastructure.Feeds;

/// <summary>
/// Leitor de RSS 2.0, RSS 1.0/RDF e Atom. O XML é lido com DTD proibido e sem resolvedor
/// externo (XXE), com limite de caracteres, e todo texto é convertido para texto puro.
/// </summary>
public sealed class FeedReader(IHttpClientFactory clients, ILogger<FeedReader> logger) : IFeedReader
{
    private const long MaxDocumentChars = 4_000_000;

    public async Task<IReadOnlyList<FeedItem>> ReadAsync(Uri feedUri, CancellationToken ct)
    {
        using var client = clients.CreateClient("feed");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MinhaBelemBot/1.0 (+https://minhabelem.com.br/coleta)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml;q=0.9, */*;q=0.5");

        using var response = await client.GetAsync(feedUri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaxDocumentChars)
            throw new InvalidOperationException("Feed excede o tamanho máximo permitido.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxDocumentChars,
            MaxCharactersFromEntities = 100_000,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            CloseInput = false
        });

        SyndicationFeed feed;
        try
        {
            feed = SyndicationFeed.Load(reader);
        }
        catch (XmlException ex)
        {
            logger.LogWarning(ex, "Feed inválido em {Uri}", feedUri);
            return Array.Empty<FeedItem>();
        }

        if (feed is null) return Array.Empty<FeedItem>();

        var items = new List<FeedItem>();
        foreach (var item in feed.Items)
        {
            ct.ThrowIfCancellationRequested();
            var title = HtmlText.Clean(item.Title?.Text, 300);
            if (string.IsNullOrWhiteSpace(title)) continue;

            var link = item.Links
                .Select(l => l.Uri)
                .FirstOrDefault(u => u is not null && u.Scheme == Uri.UriSchemeHttps)
                ?? (item.Links.FirstOrDefault()?.Uri is { } u && u.Scheme == Uri.UriSchemeHttps ? u : null);

            var summarySource = item.Summary?.Text ?? (item.Content as System.ServiceModel.Syndication.TextSyndicationContent)?.Text ?? string.Empty;
            var summary = HtmlText.ToPlainText(summarySource, 1500);
            var published = item.PublishDate != default ? item.PublishDate.UtcDateTime : (DateTime?)null;

            items.Add(new FeedItem(
                title,
                summary,
                link?.AbsoluteUri,
                HtmlText.Clean(item.Authors?.FirstOrDefault()?.Name, 240),
                published,
                ExtractImage(item) is { } image && image.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? image : null));
        }

        return items;
    }

    private static string? ExtractImage(SyndicationItem item)
    {
        // Atom: <link rel="enclosure" type="image/*">
        var enclosure = item.Links.FirstOrDefault(l =>
            string.Equals(l.RelationshipType, "enclosure", StringComparison.OrdinalIgnoreCase) &&
            l.Uri is not null && l.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true);
        if (enclosure?.Uri is not null) return enclosure.Uri.AbsoluteUri;

        // RSS 2.0 media:content
        var media = item.ElementExtensions
            .FirstOrDefault(e => e.OuterName == "content" && e.OuterNamespace.Contains("search.yahoo.com", StringComparison.OrdinalIgnoreCase))
            ?.GetObject<System.Xml.Linq.XElement>().Attribute("url")?.Value;
        return string.IsNullOrWhiteSpace(media) ? null : media;
    }
}