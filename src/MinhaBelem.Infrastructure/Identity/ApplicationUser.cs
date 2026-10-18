using Microsoft.AspNetCore.Identity;

namespace MinhaBelem.Infrastructure.Identity;

/// <summary>
/// Usuário do painel. <c>Id</c> é long para casar com as chaves estrangeiras das demais tabelas.
/// A coluna <c>enabled</c> permite suspender o acesso sem apagar o histórico de auditoria.
/// </summary>
public sealed class ApplicationUser : IdentityUser<long>
{
    public DateTime? LockoutDate { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public bool MfaEnabled { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
