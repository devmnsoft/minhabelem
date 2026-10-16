using System.Net;
using MinhaBelem.Core.Security;

namespace MinhaBelem.Infrastructure.Networking;

public interface IDnsGuard
{
    Task<bool> IsHostPublicAsync(string host, CancellationToken ct);
}

/// <summary>Resolve o hostname e exige que todos os endereços sejam públicos (barreira de SSRF).</summary>
public sealed class DnsGuard(IDnsResolver resolver, ILogger<DnsGuard> logger) : IDnsGuard
{
    public async Task<bool> IsHostPublicAsync(string host, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        if (IPAddress.TryParse(host.Trim('[', ']'), out var literal)) return SafeUrl.IsPublicAddress(literal);

        IPAddress[] addresses;
        try
        {
            addresses = await resolver.ResolveAsync(host, ct);
        }
        catch (Exception ex) when (ex is System.Net.Sockets.SocketException or ArgumentException)
        {
            logger.LogWarning(ex, "Falha ao resolver {Host}", host);
            return false;
        }

        if (addresses.Length == 0)
        {
            logger.LogWarning("Host {Host} não resolveu para nenhum endereço", host);
            return false;
        }

        foreach (var address in addresses)
        {
            if (!SafeUrl.IsPublicAddress(address))
            {
                logger.LogWarning("Bloqueado {Host}: endereço não público {Address}", host, address);
                return false;
            }
        }
        return true;
    }
}

public interface IDnsResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct);
}

public sealed class SystemDnsResolver : IDnsResolver
{
    public async Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
        => await Dns.GetHostAddressesAsync(host, ct);
}