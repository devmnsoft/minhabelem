using System.Xml;
using System.Xml.Linq;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Security;

namespace MinhaBelem.Infrastructure.Discovery;

public sealed class SitemapDiscoveryService(IHttpClientFactory httpClientFactory) : ISitemapDiscoveryService
{
    private static readonly XmlReaderSettings SafeXmlSettings = new()
    {
        Async = true,
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = 10_000_000
    };

    public async Task<IReadOnlyList<string>> DiscoverUrlsAsync(string sitemapUrlOrDomain, CancellationToken ct)
    {
        var discovered = new List<string>();
        var queue = new Queue<(string Url, int Depth)>();

        var initialUrl = sitemapUrlOrDomain.Trim();
        if (!initialUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !initialUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            initialUrl = "https://" + initialUrl.TrimEnd('/') + "/sitemap.xml";
        }

        queue.Enqueue((initialUrl, 0));
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var client = httpClientFactory.CreateClient("feed");

        while (queue.Count > 0 && discovered.Count < 250)
        {
            var (currentUrl, depth) = queue.Dequeue();
            if (!visited.Add(currentUrl)) continue;

            var validation = SafeUrl.Validate(currentUrl);
            if (!validation.IsValid || validation.Uri is null) continue;

            try
            {
                using var response = await client.GetAsync(validation.Uri, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode) continue;
                if (response.Content.Headers.ContentLength > 5_000_000) continue;

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = XmlReader.Create(stream, SafeXmlSettings);
                var doc = await XDocument.LoadAsync(reader, LoadOptions.None, ct);

                var rootName = doc.Root?.Name.LocalName;

                if (rootName == "sitemapindex" && depth < 2)
                {
                    // Index pointing to sub-sitemaps
                    foreach (var sitemapElem in doc.Descendants().Where(e => e.Name.LocalName == "sitemap"))
                    {
                        var loc = sitemapElem.Elements().FirstOrDefault(e => e.Name.LocalName == "loc")?.Value.Trim();
                        if (!string.IsNullOrWhiteSpace(loc) && SafeUrl.Validate(loc).IsValid)
                        {
                            queue.Enqueue((loc, depth + 1));
                        }
                    }
                }
                else if (rootName == "urlset")
                {
                    // Standard URL set
                    foreach (var urlElem in doc.Descendants().Where(e => e.Name.LocalName == "url"))
                    {
                        var loc = urlElem.Elements().FirstOrDefault(e => e.Name.LocalName == "loc")?.Value.Trim();
                        if (!string.IsNullOrWhiteSpace(loc))
                        {
                            var validUrl = SafeUrl.Validate(loc);
                            if (validUrl.IsValid && !discovered.Contains(loc, StringComparer.OrdinalIgnoreCase))
                            {
                                discovered.Add(loc);
                                if (discovered.Count >= 250) break;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Sitemap unavailable or malformed XML - continue gracefully without failing the entire run
            }
        }

        return discovered;
    }
}
