using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MySqlConnector;

namespace MinhaBelem.Web.Controllers;

/// <summary>
/// Envio de fotos e vídeos pela comunidade. Tudo fica pendente de conferência
/// editorial; nada é publicado automaticamente.
/// </summary>
public sealed class MediaSubmissionController(MySqlDataSource data, IWebHostEnvironment environment) : Controller
{
    private const long MaxBytes = 6_000_000;
    private string PendingStorage => Path.Combine(environment.ContentRootPath, "App_Data", "media-pending");

    [HttpGet("/fotos/enviar")]
    [HttpGet("/{culture:regex(^(pt-BR|en|fr|es|ru|zh-Hans|ja)$)}/fotos/enviar")]
    public IActionResult Index() => View("~/Views/Media/Enviar.cshtml");

    [HttpPost("/fotos/enviar"), ValidateAntiForgeryToken, EnableRateLimiting("submission"), RequestSizeLimit(8_000_000)]
    public async Task<IActionResult> Enviar(
        IFormFile? imagem,
        string? videoUrl,
        string titulo,
        string? descricao,
        string credito,
        string licenca,
        string? local,
        string? bairro,
        string? autor,
        string? email,
        bool consentimento,
        CancellationToken ct)
    {
        var temFoto = imagem is not null && imagem.Length > 0;
        var temVideo = !string.IsNullOrWhiteSpace(videoUrl);
        if (!consentimento)
        {
            TempData["MediaNotice"] = "É preciso autorizar a uso editorial da imagem.";
            return RedirectToAction(nameof(Index));
        }
        if (!temFoto && !temVideo)
        {
            TempData["MediaNotice"] = "Escolha uma foto ou informe o link de um vídeo.";
            return RedirectToAction(nameof(Index));
        }
        if (string.IsNullOrWhiteSpace(titulo) || titulo.Length > 200 ||
            (descricao?.Length ?? 0) > 500 ||
            string.IsNullOrWhiteSpace(credito) || credito.Length > 300 ||
            string.IsNullOrWhiteSpace(licenca) || licenca.Length > 500 ||
            (local?.Length ?? 0) > 180 || (bairro?.Length ?? 0) > 120 ||
            (autor?.Length ?? 0) > 120 || (email?.Length ?? 0) > 150)
        {
            TempData["MediaNotice"] = "Confira os campos: título, crédito e licença são obrigatórios.";
            return RedirectToAction(nameof(Index));
        }
        if (email is not null && !email.Contains('@'))
        {
            TempData["MediaNotice"] = "Informe um e-mail válido ou deixe em branco.";
            return RedirectToAction(nameof(Index));
        }

        string? storageKey = null;
        string? mimeType = null;
        try
        {
            if (temFoto)
            {
                if (imagem!.Length > MaxBytes)
                {
                    TempData["MediaNotice"] = "A imagem excede 6 MB.";
                    return RedirectToAction(nameof(Index));
                }
                mimeType = await DetectMimeAsync(imagem, ct);
                if (mimeType is null)
                {
                    TempData["MediaNotice"] = "Use PNG, JPEG ou WebP.";
                    return RedirectToAction(nameof(Index));
                }
                Directory.CreateDirectory(PendingStorage);
                storageKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                var path = Path.Combine(PendingStorage, storageKey);
                await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await using var input = imagem.OpenReadStream();
                await input.CopyToAsync(output, ct);
            }
            else if (temVideo)
            {
                // Vídeo externo: aceita YouTube/Vimeo e registra a origem.
                if (!System.Text.RegularExpressions.Regex.IsMatch(videoUrl!, @"^https?://[^\s]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    TempData["MediaNotice"] = "O link do vídeo deve começar com http:// ou https://.";
                    return RedirectToAction(nameof(Index));
                }
            }

            await using var conn = await data.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition("""
                INSERT INTO mb_media_submissions
                  (storage_key, mime_type, kind, title, description, credit, license, place_name, neighborhood,
                   author_name, author_email, youtube_id, source_url, status)
                VALUES
                  (@key, @mime, @kind, @title, @description, @credit, @license, @place, @neighborhood,
                   @author, @email, @youtube, @source, 'Pending')
                """, new
                {
                    key = storageKey,
                    mime = mimeType,
                    kind = temFoto ? "photo" : "video",
                    title = titulo.Trim(),
                    description = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim(),
                    credit = credito.Trim(),
                    license = licenca.Trim(),
                    place = string.IsNullOrWhiteSpace(local) ? null : local.Trim(),
                    neighborhood = string.IsNullOrWhiteSpace(bairro) ? null : bairro.Trim(),
                    author = string.IsNullOrWhiteSpace(autor) ? null : autor.Trim(),
                    email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                    youtube = YoutubeId(videoUrl),
                    source = string.IsNullOrWhiteSpace(videoUrl) ? null : videoUrl!.Trim()
                }, cancellationToken: ct));

            TempData["MediaNotice"] = "Recebemos sua foto. Ela passa por conferência editorial antes de ser publicada.";
            return RedirectToAction(nameof(Index));
        }
        catch (MySqlException)
        {
            if (storageKey is not null) TryDelete(storageKey);
            TempData["MediaNotice"] = "Não foi possível registrar o envio agora. Tente novamente em instantes.";
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            if (storageKey is not null) TryDelete(storageKey);
            throw;
        }
    }

    private void TryDelete(string storageKey)
    {
        try { System.IO.File.Delete(Path.Combine(PendingStorage, storageKey)); } catch { /* arquivo orfan */ }
    }

    private static string? YoutubeId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(url, @"(?:youtu\.be/|v=|embed/|shorts/)([A-Za-z0-9_-]{11})");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static async Task<string?> DetectMimeAsync(IFormFile file, CancellationToken ct)
    {
        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var read = await input.ReadAsync(header, ct);
        if (read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255) return "image/jpeg";
        if (read == 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP") return "image/webp";
        return null;
    }
}
