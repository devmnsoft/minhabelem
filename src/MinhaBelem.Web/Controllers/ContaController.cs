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
        if (result.Succeeded) return RedirectToAction(nameof(Mfa));
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
    public async Task<IActionResult> Perfil() => View(await users.GetUserAsync(User));

    [HttpPost("senha")]
    public async Task<IActionResult> Senha(PasswordInput model)
    {
        var user = (await users.GetUserAsync(User))!;
        if (!ModelState.IsValid) { TempData["Notice"] = "Confira os campos. A senha deve ter pelo menos 12 caracteres."; return RedirectToAction(nameof(Perfil)); }
        var result = await users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded) { TempData["Notice"] = "Não foi possível trocar a senha. Confira a senha atual e os requisitos."; return RedirectToAction(nameof(Perfil)); }
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
