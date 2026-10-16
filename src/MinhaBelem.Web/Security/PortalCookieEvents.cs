using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Infrastructure.Identity;

namespace MinhaBelem.Web.Security;

public sealed class PortalCookieEvents(UserManager<ApplicationUser> users, IUserClaimsPrincipalFactory<ApplicationUser> principals, IAdministrationRepository admin) : CookieAuthenticationEvents
{
    public override async Task SigningIn(CookieSigningInContext context)
    {
        var user = await users.GetUserAsync(context.Principal!);
        if (user is null || !user.Enabled) throw new InvalidOperationException("Usuário indisponível.");
        var id = Guid.NewGuid().ToString("N");
        ((ClaimsIdentity)context.Principal!.Identity!).AddClaim(new Claim("mb:session", id));
        await admin.CreateSessionAsync(id, user.Id, context.Properties.ExpiresUtc?.UtcDateTime ?? DateTime.UtcNow.AddHours(2), context.HttpContext.RequestAborted);
    }
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var user = await users.GetUserAsync(context.Principal!);
        var session = context.Principal!.FindFirstValue("mb:session");
        var stamp = context.Principal!.FindFirstValue(users.Options.ClaimsIdentity.SecurityStampClaimType);
        if (user is null || !user.Enabled || await users.IsLockedOutAsync(user) || stamp != user.SecurityStamp || session is null ||
            !await admin.SessionActiveAsync(session, user.Id, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }
        var fresh = await principals.CreateAsync(user);
        var identity = (ClaimsIdentity)fresh.Identity!;
        identity.AddClaim(new Claim("mb:session", session));
        if (user.TwoFactorEnabled && context.Principal!.HasClaim("amr", "mfa")) identity.AddClaim(new Claim("amr", "mfa"));
        context.ReplacePrincipal(fresh);
    }
}
