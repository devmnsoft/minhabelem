namespace MinhaBelem.Core.Abstractions;

public sealed class VisitRequest
{
    public long Id { get; set; }
    public long PlaceId { get; set; }
    public string PlaceName { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public int PartySize { get; set; }
    public DateOnly PreferredOn { get; set; }
    public string? Message { get; set; }
    public bool Handled { get; set; }
    public DateTime CreatedAt { get; set; }
}

public interface IVisitRequests
{
    Task<long> SubmitAsync(VisitRequest request, CancellationToken ct);
    Task<IReadOnlyList<VisitRequest>> PendingAsync(CancellationToken ct);
    Task<bool> HandleAsync(long id, CancellationToken ct);
}

public interface ILandmarkGuidePublisher
{
    Task<int> PublishAsync(CancellationToken ct);
}
