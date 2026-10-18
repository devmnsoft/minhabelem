namespace MinhaBelem.Core.Abstractions;

public sealed class CollectionRequest
{
    public long Id { get; set; }
    public string Kind { get; set; } = "feeds";
    public string? TargetUrl { get; set; }
    public string Status { get; set; } = "pending";
    public int Attempts { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? Error { get; set; }
}

public interface ICollectionQueue
{
    Task<long> EnqueueAsync(string kind, string? targetUrl, long actorId, CancellationToken ct);
    Task<IReadOnlyList<CollectionRequest>> RecentAsync(CancellationToken ct);
}
