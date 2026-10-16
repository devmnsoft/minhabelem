using System.Security.Cryptography;
using System.Text;

namespace MinhaBelem.Core.Text;

public static class Hashing
{
    /// <summary>Hash estável de URL normalizada (minúscula, sem fragmento, sem barra final).</summary>
    public static byte[] UrlHash(string? url)
    {
        var normalized = NormalizeUrl(url);
        return SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
    }

    public static string UrlHashHex(string? url) => Convert.ToHexString(UrlHash(url)).ToLowerInvariant();

    public static string NormalizeUrl(string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)) return (url ?? "").Trim().ToLowerInvariant();
        var builder = new UriBuilder(uri) { Fragment = "" };
        var path = builder.Path.TrimEnd('/');
        var query = string.IsNullOrEmpty(builder.Query) ? "" : builder.Query.TrimEnd('&');
        var host = builder.Host.ToLowerInvariant();
        host = host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
        return $"{uri.Scheme.ToLowerInvariant()}://{host}:{port(builder.Port)}{path}{query}";

        static int port(int p) => p == 80 || p == 443 ? 0 : p;
    }

    /// <summary>Hash de conteúdo textual usado na detecção de duplicidade exata.</summary>
    public static string ContentFingerprint(string? title, string? summary)
    {
        var payload = TextNormalizer.Normalize(title) + "|" + TextNormalizer.Normalize(summary);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static byte[] Sha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}