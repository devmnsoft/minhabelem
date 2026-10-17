using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Web.Controllers;

[Route("parceiros")]
public sealed class ParceirosController(IAdvertiserRepository advertisers) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var campaigns = await advertisers.ActiveAsync(DateTime.UtcNow, 10, ct);
        return View(campaigns);
    }

    [HttpPost("anunciar")]
    [ValidateAntiForgeryToken]
    public IActionResult Anunciar(string empresa, string contato, string email, string? telefone, string plano, string? mensagem)
    {
        if (string.IsNullOrWhiteSpace(empresa) || string.IsNullOrWhiteSpace(email))
        {
            TempData["Notice"] = "Preencha ao menos o nome da empresa e o e-mail de contato.";
            return RedirectToAction(nameof(Index));
        }

        // Sanitização e feedback
        var safeEmpresa = HtmlText.Clean(empresa, 150);
        var safeEmail = HtmlText.Clean(email, 150);
        
        TempData["Notice"] = $"Obrigado, {safeEmpresa}! Recebemos seu interesse no plano '{plano}'. Nossa equipe comercial entrará em contato.";
        return RedirectToAction(nameof(Index));
    }
}
