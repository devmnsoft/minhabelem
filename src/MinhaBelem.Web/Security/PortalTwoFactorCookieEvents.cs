using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using MinhaBelem.Infrastructure.Identity;

namespace MinhaBelem.Web.Security;

/// <summary>Bind the password-verified intermediate ticket to the current credential version.</summary>
public sealed class PortalTwoFactorCookieEvents(UserManager<ApplicationUser> users) : CookieAuthenticationEvents
{
    public const string StampClaim = "mb:two-factor-stamp";
    public override async Task SigningIn(CookieSigningInContext context)
    {
        var id = context.Principal?.Identity?.Name;
        var user = id is null ? null : await users.FindByIdAsync(id);
        if (user is null || !user.Enabled || string.IsNullOrWhiteSpace(user.SecurityStamp)) throw new InvalidOperationException("Não foi possível iniciar a autenticação em duas etapas.");
        ((ClaimsIdentity)context.Principal!.Identity!).AddClaim(new Claim(StampClaim, user.SecurityStamp));
    }
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var id = context.Principal?.Identity?.Name;
        var user = id is null ? null : await users.FindByIdAsync(id);
        var stamp = context.Principal?.FindFirstValue(StampClaim);
        if (user is null || !user.Enabled || await users.IsLockedOutAsync(user) ||
            string.IsNullOrWhiteSpace(stamp) || stamp != user.SecurityStamp)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
        }
    }
}
