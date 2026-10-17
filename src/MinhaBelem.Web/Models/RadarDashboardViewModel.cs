using MinhaBelem.Core.Entities;

namespace MinhaBelem.Web.Models;

public sealed class RadarDashboardViewModel
{
    public RadarDashboardStats Stats { get; set; } = new();
    public IReadOnlyList<DiscoveredSource> DiscoveredSources { get; set; } = Array.Empty<DiscoveredSource>();
    public int TotalDiscovered { get; set; }
    public DiscoveredSourceQuery Query { get; set; } = new();
    public IReadOnlyList<CrawlHistoryEntry> History { get; set; } = Array.Empty<CrawlHistoryEntry>();
    public IReadOnlyList<SocialProfileRegistry> SocialProfiles { get; set; } = Array.Empty<SocialProfileRegistry>();
    public IReadOnlyList<IngestionSource> Sources { get; set; } = Array.Empty<IngestionSource>();
    public IReadOnlyList<RadarOperationalSource> OperationalSources { get; set; } = Array.Empty<RadarOperationalSource>();
    public IReadOnlyList<SocialPost> SocialPosts { get; set; } = Array.Empty<SocialPost>();
}
