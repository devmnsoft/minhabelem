using System.Diagnostics;
using Dapper;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Security;

namespace MinhaBelem.Infrastructure.Discovery;

/// <summary>OCR local explicito. Exige executavel instalado e permissao de midia por fonte.</summary>
public sealed class TesseractMediaAnalysis(MySqlDataSource data, IHttpClientFactory clients, IConfiguration config) : IMediaAnalysis
{
    private string? Executable => config["Ocr:ExecutablePath"];
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Executable) && Path.IsPathFullyQualified(Executable) && File.Exists(Executable);
    public async Task<MediaAnalysisResult> AnalyzeAsync(long candidateId, long actorId, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("OCR aguarda instalação e configuração do Tesseract no servidor.");
        await using var db = await data.OpenConnectionAsync(ct);
        var snapshot = await db.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT v.snapshot FROM mb_discovery_versions v JOIN mb_ingestion_candidates c ON c.id=v.candidate_id JOIN mb_media_permissions p ON p.source_id=c.source_id AND p.allow_analysis=1 AND p.license_note<>'' WHERE v.candidate_id=@candidateId ORDER BY v.id DESC LIMIT 1", new { candidateId }, cancellationToken: ct));
        if (snapshot is null) throw new InvalidOperationException("A fonte não possui autorização de análise de mídia.");
        var item = System.Text.Json.JsonSerializer.Deserialize<DiscoveredItem>(snapshot)!;
        var valid = SafeUrl.Validate(item.MediaUrl);
        var allowedHosts = config.GetSection("Ocr:AllowedMediaHosts").Get<string[]>() ?? Array.Empty<string>();
        if (!valid.IsValid || !allowedHosts.Contains(valid.Uri!.DnsSafeHost, StringComparer.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(item.MediaCredit) || string.IsNullOrWhiteSpace(item.MediaLicense)) throw new InvalidOperationException("Mídia sem licença, crédito ou host autorizado.");
        using var response = await clients.CreateClient("feed").GetAsync(valid.Uri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is not ("image/png" or "image/jpeg" or "video/mp4")) throw new InvalidOperationException("Formato de mídia não suportado. Use PNG, JPEG ou MP4 autorizado.");
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        using var image = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await source.ReadAsync(buffer, ct)) != 0)
        { if (image.Length + count > (contentType == "video/mp4" ? 10_000_000 : 2_000_000)) throw new InvalidOperationException("Mídia excede o limite de tamanho."); await image.WriteAsync(buffer.AsMemory(0, count), ct); }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var thumbnail = image.ToArray();
        if (contentType == "video/mp4")
        {
            thumbnail = await MediaProcess.RunAsync(config["Ocr:FfmpegPath"] ?? "", new[] { "-nostdin", "-loglevel", "error", "-i", "pipe:0", "-frames:v", "1", "-vf", "scale=1200:-2", "-f", "image2pipe", "-vcodec", "png", "pipe:1" }, thumbnail, 3_000_000, timeout.Token);
            contentType = "image/png";
        }
        var output = await MediaProcess.RunAsync(Executable!, new[] { "stdin", "stdout", "-l", "por" }, thumbnail, 32000, timeout.Token);
        var text = Core.Text.HtmlText.Clean(System.Text.Encoding.UTF8.GetString(output), 8000);
        await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_media_analysis (candidate_id,actor_id,engine,extracted_text,credit,license_note) VALUES (@candidateId,@actorId,'tesseract',@text,@credit,@license)", new { candidateId, actorId, text, credit = item.MediaCredit, license = item.MediaLicense }, cancellationToken: ct));
        return new(text, "data:" + contentType + ";base64," + Convert.ToBase64String(thumbnail), "Mídia autorizada com texto extraído, ainda não verificado: " + Core.Text.HtmlText.Truncate(text, 180), item.MediaCredit!, item.MediaLicense!);
    }
}
