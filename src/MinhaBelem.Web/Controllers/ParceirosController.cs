using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Text;
using Microsoft.Extensions.Localization;
using MinhaBelem.Web.Resources;
using System.ComponentModel.DataAnnotations;

namespace MinhaBelem.Web.Controllers;

[Route("parceiros")]
public sealed class ParceirosController(IAdvertiserRepository advertisers, IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var campaigns = await advertisers.ActiveAsync(DateTime.UtcNow, 10, ct);
        return View(campaigns);
    }

    [HttpPost("anunciar")]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("submission")]
    public async Task<IActionResult> Anunciar(string empresa, string contato, string email, string? telefone, string plano, string? mensagem, bool consentimento, CancellationToken ct)
    {
        if (!consentimento || string.IsNullOrWhiteSpace(empresa) || empresa.Length > 150 ||
            string.IsNullOrWhiteSpace(contato) || contato.Length > 100 || string.IsNullOrWhiteSpace(email) ||
            email.Length > 150 || !new EmailAddressAttribute().IsValid(email) || telefone?.Length > 30 ||
            mensagem?.Length > 2000 || plano is not ("Guia" or "Categoria" or "Bairro" or "Personalizado"))
        {
            TempData["Notice"] = loc["CommercialInvalid"].Value;
            return RedirectToAction(nameof(Index));
        }

        await advertisers.SubmitInquiryAsync(new()
        {
            Company = HtmlText.Clean(empresa, 150), ContactName = HtmlText.Clean(contato, 100),
            Email = email.Trim(), Phone = HtmlText.Clean(telefone, 30), Plan = plano,
            Message = HtmlText.ToPlainText(mensagem, 2000)
        }, ct);
        TempData["Notice"] = loc["CommercialReceived"].Value;
        return RedirectToAction(nameof(Index));
    }
}
