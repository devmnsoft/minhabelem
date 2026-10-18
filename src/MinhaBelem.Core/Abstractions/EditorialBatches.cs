using System.Security.Claims;
namespace MinhaBelem.Core.Abstractions;

public sealed record BatchTarget(long Id, string Kind, long Revision);
public sealed class EditorialBatchInput
{
    public string Action { get; set; } = "review";
    public string Reason { get; set; } = "";
    public string? Value { get; set; }
    public string? Municipality { get; set; }
    public string? Neighborhood { get; set; }
    public BatchTarget[] Targets { get; set; } = [];
}
public sealed class EditorialBatch
{
    public string Id { get; set; } = "";
    public long ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<EditorialBatchItem> Items { get; set; } = [];
}
public sealed class EditorialBatchItem
{
    public long TargetId { get; set; }
    public string TargetKind { get; set; } = "";
    public long Revision { get; set; }
    public string Status { get; set; } = "";
    public string? Result { get; set; }
    public string Payload { get; set; } = "";
}
public interface IEditorialBatches
{
    Task<IReadOnlyDictionary<string,long>> DashboardAsync(CancellationToken ct);
    Task<string> PreviewAsync(EditorialBatchInput input, ClaimsPrincipal actor, CancellationToken ct);
    Task<EditorialBatch?> GetAsync(string id, long actorId, CancellationToken ct);
    Task<bool> ConfirmAsync(string id, ClaimsPrincipal actor, CancellationToken ct);
    Task<bool> ProcessNextAsync(CancellationToken ct);
}
