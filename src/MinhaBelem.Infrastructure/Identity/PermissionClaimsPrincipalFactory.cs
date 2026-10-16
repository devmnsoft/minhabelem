using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Identity;
using MinhaBelem.Core.Security;

namespace MinhaBelem.Infrastructure.Identity;

/// <summary>
/// Injeta as permissões efetivas (união dos papéis do usuário) na identidade do cookie.
/// O painel autoriza por permissão, não apenas por papel.
/// </summary>
public sealed class PermissionClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<long>> roleManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<long>>(userManager, roleManager, options)
{
    public const string PermissionClaimType = "mb:permission";
    public const string MfaClaimType = "mb:mfa";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var userId = await UserManager.GetUserIdAsync(user);
        identity.AddClaim(new Claim("mb:uid", userId));

        var roles = await UserManager.GetRolesAsync(user);
        var permissions = Permissions.ByRole
            .Where(pair => roles.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        foreach (var permission in permissions)
            identity.AddClaim(new Claim(PermissionClaimType, permission));

        identity.AddClaim(new Claim(MfaClaimType, user.TwoFactorEnabled ? "1" : "0"));
        return identity;
    }
}