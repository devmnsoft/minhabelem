using System.Net;
using System.Net.Sockets;

namespace MinhaBelem.Core.Security;

public sealed record UrlValidationResult(bool IsValid, string? Reason, Uri? Uri)
{
    public static UrlValidationResult Ok(Uri uri) => new(true, null, uri);
    public static UrlValidationResult Fail(string reason) => new(false, reason, null);
}

/// <summary>
/// Validação de URL usada antes de qualquer requisição de saída (coleta, Places, imagens).
/// É a barreira de SSRF: esquema restrito, sem credenciais embutidas, sem porta exótica,
/// sem IP privado/literal e hostname obrigatório. A checagem de DNS resolvido acontece
/// em <see cref="ResolveAndValidateHostAsync"/>, na camada de infraestrutura.
/// </summary>
public static class SafeUrl
{
    public const int MaxUrlLength = 2000;

    public static UrlValidationResult Validate(string? raw, IReadOnlySet<string>? allowedHosts = null)
    {
        if (string.IsNullOrWhiteSpace(raw)) return UrlValidationResult.Fail("URL vazia.");
        var value = raw.Trim();
        if (value.Length > MaxUrlLength) return UrlValidationResult.Fail("URL excede o tamanho máximo.");
        if (value.Any(char.IsControl)) return UrlValidationResult.Fail("URL contém caracteres de controle.");

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return UrlValidationResult.Fail("URL absoluta inválida.");

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return UrlValidationResult.Fail("Somente HTTPS é aceito.");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            return UrlValidationResult.Fail("URL não pode conter credenciais.");

        var host = uri.DnsSafeHost;
        if (string.IsNullOrWhiteSpace(host)) return UrlValidationResult.Fail("Host ausente.");
        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".test", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            return UrlValidationResult.Fail("Host não permitido.");

        if (IPAddress.TryParse(host.Trim('[', ']'), out var ip))
        {
            if (!IsPublicAddress(ip)) return UrlValidationResult.Fail("Endereço IP não público.");
        }
        else if (!IsPlausibleHostname(host))
        {
            return UrlValidationResult.Fail("Hostname inválido.");
        }

        if (uri.Port is not (-1 or 80 or 443)) return UrlValidationResult.Fail("Porta não permitida.");
        if (allowedHosts is { Count: > 0 } && !allowedHosts.Contains(host.ToLowerInvariant()))
            return UrlValidationResult.Fail("Host não está na lista permitida.");

        return UrlValidationResult.Ok(uri);
    }

    private static bool IsPlausibleHostname(string host)
    {
        if (host.Length is 0 or > 253) return false;
        if (!host.Contains('.')) return false;
        var labels = host.Split('.');
        if (labels.Length < 2) return false;
        foreach (var label in labels)
        {
            if (label.Length is 0 or > 63) return false;
            if (label.StartsWith('-') || label.EndsWith('-')) return false;
            if (!label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')) return false;
        }
        var tld = labels[^1];
        return tld.Length >= 2 && tld.All(char.IsAsciiLetter);
    }

    /// <summary>Rejeita loopback, link-local, privados, reservados e IPv4 mapeado em IPv6.</summary>
    public static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return false;
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return false;
        if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
            return IsPublicAddress(address.MapToIPv4());

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                0 => false,                             // 0.0.0.0/8
                10 => false,                            // 10/8
                127 => false,                           // loopback
                100 when bytes[1] is >= 64 and <= 127 => false, // CGNAT 100.64/10
                169 when bytes[1] == 254 => false,      // link-local
                172 when bytes[1] is >= 16 and <= 31 => false, // 172.16/12
                192 when bytes[1] == 168 => false,       // 192.168/16
                192 when bytes[1] == 0 => false,        // IETF 192.0.0/24
                198 when bytes[1] is 18 or 19 => false, // benchmarking
                198 when bytes[1] == 51 => false,       // TEST-NET-2
                203 when bytes[1] == 0 => false,        // TEST-NET-3
                >= 224 => false,                        // multicast/reservado
                _ => true
            };
        }

        var v6 = address.GetAddressBytes();
        // Apenas unicast global 2000::/3; bloqueia ULA, link-local, multicast e documentacao.
        if ((v6[0] & 0xE0) != 0x20) return false;
        if (v6[0] == 0x20 && v6[1] == 0x01 &&
            ((v6[2] == 0x0d && v6[3] == 0xb8) || (v6[2] == 0 && v6[3] <= 0x2f))) return false;
        return true;
    }
}
