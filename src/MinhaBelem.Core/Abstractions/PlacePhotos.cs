namespace MinhaBelem.Core.Abstractions;

public sealed record PageImageCapture(string PageUrl, string ImageUrl, string? Credit, string? License, string? Title);

public sealed class PublicPlacePhoto
{
    public long Id { get; set; }
    public long? PlaceId { get; set; }
    public string PlaceName { get; set; } = "";
    public string Municipality { get; set; } = "";
    public string ThumbUrl { get; set; } = "";
    public string PageUrl { get; set; } = "";
    public string Credit { get; set; } = "";
    public string LicenseName { get; set; } = "";
    public string? LicenseUrl { get; set; }
    public string Status { get; set; } = "";
    public bool Publishable { get; set; }
}

public sealed class ResidentEventChange
{
    public long Id { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
}

public interface IPlacePhotoScout
{
    Task<int> ScoutNextAsync(int maxTargets, CancellationToken ct);
    Task<int> CapturePageImageAsync(PageImageCapture capture, CancellationToken ct);
    Task<IReadOnlyList<PublicPlacePhoto>> ApprovedAsync(long? placeId, string? placeName, CancellationToken ct);
    Task<IReadOnlyDictionary<long, PublicPlacePhoto>> CoversAsync(IReadOnlyCollection<long> placeIds, CancellationToken ct);
    Task<(int Approved, int Pending, IReadOnlyList<PublicPlacePhoto> ApprovedRows, IReadOnlyList<PublicPlacePhoto> PendingRows)> DeskAsync(CancellationToken ct);
    Task<bool> ReviewAsync(long id, bool approve, long actorId, CancellationToken ct);
    Task ReplaceSavesAsync(string deviceKey, IReadOnlyList<long> eventIds, CancellationToken ct);
    Task<IReadOnlyList<ResidentEventChange>> ChangesAsync(IReadOnlyList<long> eventIds, CancellationToken ct);
}
