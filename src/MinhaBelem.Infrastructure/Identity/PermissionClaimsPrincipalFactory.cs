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
    RoleManager<IdentityRole<long>> roleManager, IOptions<IdentityOptions> options, Core.Abstractions.IAdministrationRepository admin)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<long>>(userManager, roleManager, options)
{
    public const string PermissionClaimType = "mb:permission";
    public const string MfaClaimType = "mb:mfa";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var userId = await UserManager.GetUserIdAsync(user);
        identity.AddClaim(new Claim("mb:uid", userId));

        var permissions = (await admin.PermissionsAsync(user.Id, default))
            .Where(code => Permissions.All.Contains(code))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        foreach (var permission in permissions)
            identity.AddClaim(new Claim(PermissionClaimType, permission));

        return identity;
    }
}
