using System.Net.Sockets;
using MinhaBelem.Core.Security;

namespace MinhaBelem.Infrastructure.Networking;

public static class PublicHttpTransport
{
    public static SocketsHttpHandler Create(IServiceProvider services) => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await services.GetRequiredService<IDnsResolver>().ResolveAsync(context.DnsEndPoint.Host, ct);
            if (addresses.Length == 0 || addresses.Any(ip => !SafeUrl.IsPublicAddress(ip))) throw new HttpRequestException("Destino não público bloqueado.");
            // Conectar ao IP validado impede uma segunda resolucao de DNS (rebinding).
            Exception? error = null;
            foreach (var address in addresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try { await socket.ConnectAsync(address, context.DnsEndPoint.Port, ct); return new NetworkStream(socket, ownsSocket: true); }
                catch (Exception ex) when (ex is SocketException) { socket.Dispose(); error = ex; }
                catch { socket.Dispose(); throw; }
            }
            throw new HttpRequestException("Não foi possível conectar ao destino público.", error);
        }
    };
}
