using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Infrastructure.Identity;
using MinhaBelem.Web.Models;

namespace MinhaBelem.Web.Controllers;

[Route("conta"), Authorize]
public sealed class ContaController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, IAdministrationRepository admin, IAuditRepository audit) : Controller
{
    [AllowAnonymous, HttpGet("login")]
    public IActionResult Login() => View(new LoginInput());

    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginInput model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await users.FindByEmailAsync(model.Email);
        var result = user is { Enabled: true }
            ? await signIn.PasswordSignInAsync(user, model.Password, false, true)
            : Microsoft.AspNetCore.Identity.SignInResult.Failed;
        if (result.RequiresTwoFactor) return RedirectToAction(nameof(Verificar));
        if (result.Succeeded)
        {
            user!.LastLoginAt = DateTime.UtcNow;
            await users.UpdateAsync(user);
            await audit.WriteAsync(new AuditEntry { ActorId = user.Id, Action = "session.password_login", EntityType = "user", EntityId = user.Id }, HttpContext.RequestAborted);
            return RedirectToAction(user.MustChangePassword ? nameof(Perfil) : nameof(Mfa));
        }
        ModelState.AddModelError("", "Não foi possível entrar. Confira os dados ou aguarde antes de tentar novamente.");
        model.Password = "";
        return View(model);
    }

    [AllowAnonymous, HttpGet("verificar")]
    public async Task<IActionResult> Verificar() => await signIn.GetTwoFactorAuthenticationUserAsync() is null ? RedirectToAction(nameof(Login)) : View();

    [AllowAnonymous, HttpPost("verificar"), EnableRateLimiting("login")]
    public async Task<IActionResult> Verificar(string code, bool recovery = false)
    {
        var user = await signIn.GetTwoFactorAuthenticationUserAsync();
        if (user is null || !user.Enabled || string.IsNullOrWhiteSpace(code) || code.Length > 30) return RedirectToAction(nameof(Login));
        var result = recovery
            ? await signIn.TwoFactorRecoveryCodeSignInAsync(code.Trim())
            : await signIn.TwoFactorAuthenticatorSignInAsync(code.Trim(), false, false);
        if (!result.Succeeded) { ModelState.AddModelError("", "Código inválido ou acesso temporariamente bloqueado."); return View(); }
        user.LastLoginAt = DateTime.UtcNow;
        await users.UpdateAsync(user);
        await audit.WriteAsync(new AuditEntry { ActorId = user.Id, Action = "session.mfa_login", EntityType = "user", EntityId = user.Id }, HttpContext.RequestAborted);
        return RedirectToAction("Index", "Editorial");
    }

    [HttpGet("mfa")]
    public async Task<IActionResult> Mfa()
    {
        var user = (await users.GetUserAsync(User))!;
        var secret = await users.GetAuthenticatorKeyAsync(user);
        return View(new MfaModel(user.TwoFactorEnabled ? null : secret, secret is null || user.TwoFactorEnabled ? null : Totp.BuildSetup(user.Email!, "MinhaBelém", secret).OtpAuthUri, user.TwoFactorEnabled));
    }

    [HttpPost("mfa/iniciar")]
    public async Task<IActionResult> IniciarMfa()
    {
        var user = (await users.GetUserAsync(User))!;
        if (user.TwoFactorEnabled) return BadRequest();
        var result = await users.ResetAuthenticatorKeyAsync(user);
        if (!result.Succeeded) return BadRequest("Não foi possível preparar o autenticador.");
        await admin.RevokeSessionAsync(User.FindFirst("mb:session")!.Value, user.Id, HttpContext.RequestAborted);
        await signIn.RefreshSignInAsync(user);
        return RedirectToAction(nameof(Mfa));
    }

    [HttpPost("mfa"), EnableRateLimiting("login")]
    public async Task<IActionResult> ConfirmarMfa(string code)
    {
        var user = (await users.GetUserAsync(User))!;
        if (user.TwoFactorEnabled || code is null || code.Length != 6 || !code.All(char.IsAsciiDigit) ||
            !await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code))
        { TempData["Notice"] = "Código inválido. Confira o relógio e o aplicativo autenticador."; return RedirectToAction(nameof(Mfa)); }
        var result = await users.SetTwoFactorEnabledAsync(user, true);
        if (!result.Succeeded) return BadRequest("Não foi possível ativar o MFA.");
        var codes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 8))!.ToArray();
        await audit.WriteAsync(new AuditEntry { ActorId = user.Id, Action = "user.mfa_enabled", EntityType = "user", EntityId = user.Id }, HttpContext.RequestAborted);
        await admin.RevokeSessionAsync(User.FindFirst("mb:session")!.Value, user.Id, HttpContext.RequestAborted);
        await signIn.SignOutAsync();
        return View("Recovery", codes);
    }

    [HttpGet("perfil")]
    public async Task<IActionResult> Perfil()
    {
        var user=(await users.GetUserAsync(User))!;
        var preferences=await users.GetClaimsAsync(user);
        ViewData["PreferredCulture"]=preferences.FirstOrDefault(x=>x.Type=="mb:preference:culture")?.Value;
        ViewData["PrefersAccessible"]=preferences.Any(x=>x.Type=="mb:preference:accessible"&&x.Value=="true");
        return View(user);
    }
    [HttpPost("preferencias")]
    public async Task<IActionResult> Preferencias(string culture,bool accessible)
    {
        if(!MinhaBelem.Web.Resources.SupportedCultures.All.Contains(culture))return BadRequest();
        var user=(await users.GetUserAsync(User))!;
        var existing=await users.GetClaimsAsync(user);
        foreach(var (type,value) in new[]{("mb:preference:culture",culture),("mb:preference:accessible",accessible?"true":"false")})
        {
            var old=existing.FirstOrDefault(c=>c.Type==type);
            var claim=new System.Security.Claims.Claim(type,value);
            var result=old is null?await users.AddClaimAsync(user,claim):await users.ReplaceClaimAsync(user,old,claim);
            if(!result.Succeeded)return Conflict();
        }
        await audit.WriteAsync(new AuditEntry{ActorId=user.Id,Action="user.preferences_updated",EntityType="user",EntityId=user.Id},HttpContext.RequestAborted);
        Response.Cookies.Append(Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.DefaultCookieName,Microsoft.AspNetCore.Localization.CookieRequestCultureProvider.MakeCookieValue(new Microsoft.AspNetCore.Localization.RequestCulture(culture)),new CookieOptions{Secure=true,HttpOnly=true,SameSite=SameSiteMode.Strict,Expires=DateTimeOffset.UtcNow.AddYears(1)});
        return RedirectToAction(nameof(Perfil));
    }

    [HttpPost("senha")]
    public async Task<IActionResult> Senha(PasswordInput model)
    {
        var user = (await users.GetUserAsync(User))!;
        if (!ModelState.IsValid) { TempData["Notice"] = "Confira os campos. A senha deve ter pelo menos 12 caracteres."; return RedirectToAction(nameof(Perfil)); }
        var result = await users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded) { TempData["Notice"] = "Não foi possível trocar a senha. Confira a senha atual e os requisitos."; return RedirectToAction(nameof(Perfil)); }
        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTime.UtcNow;
        if (!(await users.UpdateAsync(user)).Succeeded) return Conflict();
        await audit.WriteAsync(new AuditEntry { ActorId = user.Id, Action = "user.password_changed", EntityType = "user", EntityId = user.Id }, HttpContext.RequestAborted);
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("sessoes")]
    public async Task<IActionResult> Sessoes() => View(await admin.SessionsAsync((await users.GetUserAsync(User))!.Id, HttpContext.RequestAborted));
    [HttpPost("sessoes/revogar")]
    public async Task<IActionResult> Revogar(string id)
    {
        await admin.RevokeSessionAsync(id, (await users.GetUserAsync(User))!.Id, HttpContext.RequestAborted);
        return RedirectToAction(nameof(Sessoes));
    }
    [HttpPost("sair")]
    public async Task<IActionResult> Sair()
    {
        await admin.RevokeSessionAsync(User.FindFirst("mb:session")!.Value, (await users.GetUserAsync(User))!.Id, HttpContext.RequestAborted);
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
    [AllowAnonymous, HttpGet("acesso-negado")]
    public IActionResult AcessoNegado() { Response.StatusCode = 403; return View(); }
}
