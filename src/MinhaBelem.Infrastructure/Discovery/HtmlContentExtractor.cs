using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Infrastructure.Discovery;

public sealed partial class HtmlContentExtractor : IHtmlContentExtractor
{
    private static readonly string[] RecognizedBelemTerms = new[]
    {
        "Belém", "Belem", "Pará", "Paraense", "Ananindeua", "Icoaraci", "Mosqueiro", "Cotijuba", "Combu",
        "Ver-o-Peso", "Theatro da Paz", "Teatro da Paz", "Mangal das Garças", "Estação das Docas",
        "Basílica de Nazaré", "Basílica Santuário", "Círio", "Cirio", "Centur", "Hangar", "Fumbel",
        "Secult", "Casa das Onze Janelas", "Forte do Presépio", "Bosque Rodrigues Alves", "Ilha do Combu",
        "Portal da Amazônia", "Praça da República", "Praça Batista Campos", "Praça Brasil", "Doca"
    };

    [GeneratedRegex(@"<title\b[^>]*>([\s\S]*?)</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<meta\b[^>]*(?:name|property)\s*=\s*['""]([^'""]+)['""][^>]*content\s*=\s*['""]([^'""]*)['""][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MetaPropertyRegex();

    [GeneratedRegex(@"<meta\b[^>]*content\s*=\s*['""]([^'""]*)['""][^>]*(?:name|property)\s*=\s*['""]([^'""]+)['""][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MetaContentFirstRegex();

    [GeneratedRegex(@"<link\b[^>]*rel\s*=\s*['""]canonical['""][^>]*href\s*=\s*['""]([^'""]+)['""][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex CanonicalRegex();

    [GeneratedRegex(@"<time\b[^>]*datetime\s*=\s*['""]([^'""]+)['""][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex TimeDatetimeRegex();

    [GeneratedRegex(@"<a\b[^>]*href\s*=\s*['""]([^'""]+)['""][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex AnchorHrefRegex();

    public ExtractedHtmlContent Extract(string html, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(html)) return new ExtractedHtmlContent();

        var metaDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in MetaPropertyRegex().Matches(html))
        {
            var key = match.Groups[1].Value.Trim();
            var val = WebUtility.HtmlDecode(match.Groups[2].Value.Trim());
            if (!metaDict.ContainsKey(key)) metaDict[key] = val;
        }

        foreach (Match match in MetaContentFirstRegex().Matches(html))
        {
            var key = match.Groups[2].Value.Trim();
            var val = WebUtility.HtmlDecode(match.Groups[1].Value.Trim());
            if (!metaDict.ContainsKey(key)) metaDict[key] = val;
        }

        // Title extraction
        var title = metaDict.GetValueOrDefault("og:title")
            ?? metaDict.GetValueOrDefault("twitter:title")
            ?? (TitleRegex().Match(html) is { Success: true } tm ? WebUtility.HtmlDecode(tm.Groups[1].Value) : null);
        title = HtmlText.Clean(title, 300);

        // Summary extraction
        var summary = metaDict.GetValueOrDefault("og:description")
            ?? metaDict.GetValueOrDefault("twitter:description")
            ?? metaDict.GetValueOrDefault("description");
        if (string.IsNullOrWhiteSpace(summary))
        {
            var plain = HtmlText.ToPlainText(html, 1500);
            summary = HtmlText.Truncate(plain, 500);
        }
        else
        {
            summary = HtmlText.ToPlainText(summary, 1500);
        }

        // Canonical URL
        var canonical = CanonicalRegex().Match(html) is { Success: true } cm ? cm.Groups[1].Value.Trim() : null;
        canonical ??= metaDict.GetValueOrDefault("og:url");

        // Author
        var author = metaDict.GetValueOrDefault("author")
            ?? metaDict.GetValueOrDefault("article:author")
            ?? metaDict.GetValueOrDefault("twitter:creator");
        author = HtmlText.Clean(author, 180);

        // Published date
        DateTime? publishedAt = null;
        var dateRaw = metaDict.GetValueOrDefault("article:published_time")
            ?? metaDict.GetValueOrDefault("og:published_time")
            ?? metaDict.GetValueOrDefault("datePublished")
            ?? metaDict.GetValueOrDefault("pubdate")
            ?? (TimeDatetimeRegex().Match(html) is { Success: true } timeMatch ? timeMatch.Groups[1].Value.Trim() : null);

        if (!string.IsNullOrWhiteSpace(dateRaw) && DateTimeOffset.TryParse(dateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedOffset))
        {
            publishedAt = parsedOffset.UtcDateTime;
        }

        // Lead image
        var leadImage = metaDict.GetValueOrDefault("og:image")
            ?? metaDict.GetValueOrDefault("twitter:image");

        // Outbound links
        var outboundLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match anchor in AnchorHrefRegex().Matches(html))
        {
            var rawHref = anchor.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(rawHref) || rawHref.StartsWith('#') || rawHref.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) || rawHref.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                continue;

            if (Uri.TryCreate(baseUri, rawHref, out var resolvedUri) && (resolvedUri.Scheme == Uri.UriSchemeHttp || resolvedUri.Scheme == Uri.UriSchemeHttps))
            {
                var urlString = resolvedUri.GetLeftPart(UriPartial.Path);
                if (SafeUrl.Validate(urlString).IsValid && !IsBinaryResource(resolvedUri.AbsolutePath))
                {
                    outboundLinks.Add(urlString);
                }
            }
        }

        // Relevancy to Belém / Pará
        var rawClean = HtmlText.ToPlainText(html, 10_000);
        var searchContext = rawClean + " " + (title ?? "") + " " + (summary ?? "");
        var keywordsFound = new List<string>();

        foreach (var term in RecognizedBelemTerms)
        {
            if (searchContext.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                keywordsFound.Add(term);
            }
        }

        foreach (var neighborhood in BelemGeography.BelemNeighborhoods)
        {
            if (searchContext.Contains(neighborhood, StringComparison.OrdinalIgnoreCase))
            {
                if (!keywordsFound.Contains(neighborhood, StringComparer.OrdinalIgnoreCase))
                    keywordsFound.Add(neighborhood);
            }
        }

        return new ExtractedHtmlContent
        {
            Title = title ?? string.Empty,
            Summary = summary ?? string.Empty,
            CanonicalUrl = canonical,
            Author = string.IsNullOrWhiteSpace(author) ? null : author,
            PublishedAt = publishedAt,
            LeadImageUrl = leadImage,
            LeadImageCredit = metaDict.GetValueOrDefault("og:image:alt"),
            RawCleanText = rawClean,
            OutboundLinks = outboundLinks.Take(50).ToArray(),
            MetaTags = metaDict,
            BelemKeywordsFound = keywordsFound
        };
    }

    private static bool IsBinaryResource(string path)
    {
        var ext = Path.GetExtension(path)?.ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".svg" or ".pdf" or ".zip" or ".mp4" or ".mp3" or ".css" or ".js";
    }
}
