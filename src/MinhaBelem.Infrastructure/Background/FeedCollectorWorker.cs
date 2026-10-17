using System.Diagnostics;
using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinhaBelem.Infrastructure.Background;

public sealed class CollectorOptions
{
    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 10;
    public int MaxItemsPerFeed { get; set; } = 40;
    public int MaxConcurrentSources { get; set; } = 4;
}

public sealed record FeedReadResult(IReadOnlyList<FeedItem> Items, string? ETag, DateTime? LastModified);

public interface IFeedClient
{
    Task<FeedReadResult> ReadAsync(Uri feedUri, string? etag, DateTime? lastModified, CancellationToken ct);
}

/// <summary>Cliente HTTP de feed com ETag/Last-Modified e proteção SSRF.</summary>
public sealed class HttpFeedClient(HttpClient http) : IFeedClient
{
    public async Task<FeedReadResult> ReadAsync(Uri feedUri, string? etag, DateTime? lastModified, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, feedUri);
        request.Headers.UserAgent.ParseAdd("MinhaBelemBot/1.0 (+https://minhabelem.com.br/coleta)");
        request.Headers.Accept.ParseAdd("application/rss+xml, application/atom+xml, application/xml;q=0.9, */*;q=0.5");
        if (!string.IsNullOrWhiteSpace(etag)) request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        if (lastModified is not null) request.Headers.IfModifiedSince = lastModified.Value;

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var newEtag = response.Headers.ETag?.Tag;
        var newLastModified = response.Content.Headers.LastModified?.UtcDateTime;

        if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
            return new FeedReadResult(Array.Empty<FeedItem>(), newEtag, newLastModified);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > 4_000_000)
            throw new InvalidOperationException("Feed excede o tamanho máximo permitido.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings
        {
            DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 4_000_000,
            MaxCharactersFromEntities = 100_000,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            CloseInput = false
        });

        System.ServiceModel.Syndication.SyndicationFeed? feed;
        try { feed = System.ServiceModel.Syndication.SyndicationFeed.Load(reader); }
        catch (System.Xml.XmlException) { return new FeedReadResult(Array.Empty<FeedItem>(), newEtag, newLastModified); }

        if (feed is null) return new FeedReadResult(Array.Empty<FeedItem>(), newEtag, newLastModified);

        var items = feed.Items.Select(item =>
        {
            var title = HtmlText.Clean(item.Title?.Text, 300);
            var link = item.Links.Select(l => l.Uri).FirstOrDefault(u => u is not null && u.Scheme == Uri.UriSchemeHttps);
            var summary = HtmlText.ToPlainText(item.Summary?.Text ?? (item.Content as System.ServiceModel.Syndication.TextSyndicationContent)?.Text ?? string.Empty, 1500);
            return new FeedItem(
                title,
                summary,
                link?.AbsoluteUri,
                HtmlText.Clean(item.Authors?.FirstOrDefault()?.Name, 240),
                item.PublishDate != default ? item.PublishDate.UtcDateTime : null,
                null);
        }).Where(i => i.Title.Length > 0).ToArray();

        return new FeedReadResult(items, newEtag, newLastModified);
    }
}

