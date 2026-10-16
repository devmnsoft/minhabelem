using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
using MinhaBelem.Infrastructure.Background;
namespace MinhaBelem.Infrastructure.Discovery;
public sealed class RssAtomConnector(IFeedClient feed) : IRssAtomConnector
{
    public bool Supports(FeedProtocol protocol) => protocol is FeedProtocol.Rss or FeedProtocol.Atom;
    public string? ConfigurationIssue(SourceContext context) => null;
    public async Task<ConnectorResult> ReadAsync(SourceContext context, CancellationToken ct)
    {
        var result = await feed.ReadAsync(new Uri(context.Source.FeedUrl), context.Source.ETag, context.Source.LastModified, ct);
        return new(result.Items.Where(x => x.Link is not null).Select(x => new DiscoveredItem(x.Title, x.Summary, x.Link!, x.Author, x.PublishedAt)).ToArray(), result.ETag, result.LastModified);
    }
}
