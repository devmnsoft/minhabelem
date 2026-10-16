using MinhaBelem.Core.Entities;

namespace MinhaBelem.Core.Abstractions;

public sealed class UserSession
{
    public string Id { get; set; } = "";
    public long UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
public sealed class AdminUser
{
    public long Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Enabled { get; set; }
    public bool TwoFactorEnabled { get; set; }
}
public interface IAdministrationRepository
{
    Task<IReadOnlyList<AdminUser>> UsersAsync(CancellationToken ct);
    Task<IReadOnlyList<UserSession>> SessionsAsync(long userId, CancellationToken ct);
    Task CreateSessionAsync(string id, long userId, DateTime expires, CancellationToken ct);
    Task<bool> SessionActiveAsync(string id, long userId, CancellationToken ct);
    Task RevokeSessionAsync(string id, long userId, CancellationToken ct);
    Task<IReadOnlyDictionary<string, string>> SettingsAsync(CancellationToken ct);
    Task SetSettingAsync(string key, string value, long actorId, CancellationToken ct);
    Task<IReadOnlyList<IngestionRun>> RunsAsync(CancellationToken ct);
    Task SetMediaPermissionAsync(long sourceId, bool allowed, string license, long actorId, CancellationToken ct);
    Task<IReadOnlyList<string>> PermissionsAsync(long userId, CancellationToken ct);
    Task<IReadOnlyDictionary<string, string[]>> RolePermissionsAsync(CancellationToken ct);
    Task SetRolePermissionsAsync(string role, string[] permissions, long actorId, CancellationToken ct);
}
