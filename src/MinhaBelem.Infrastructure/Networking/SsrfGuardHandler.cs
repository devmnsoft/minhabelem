using MinhaBelem.Core.Security;

namespace MinhaBelem.Infrastructure.Networking;

/// <summary>
/// Aplica a validação de URL em toda requisição de saída dos clientes HTTP do servidor.
/// Mesmo que uma URL maliciosa entre no banco, ela não chega à rede interna.
/// </summary>
public sealed class SsrfGuardHandler(IDnsGuard dnsGuard) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var validation = SafeUrl.Validate(request.RequestUri?.AbsoluteUri);
        if (!validation.IsValid || validation.Uri is null)
            throw new InvalidOperationException($"Requisição bloqueada pela proteção SSRF: {validation.Reason}");

        if (!await dnsGuard.IsHostPublicAsync(validation.Uri.DnsSafeHost, cancellationToken))
            throw new InvalidOperationException($"Requisição bloqueada: host '{validation.Uri.DnsSafeHost}' não é público.");

        var response = await base.SendAsync(request, cancellationToken);

        // Segue para o redirecionamento apenas se o destino passar pela mesma validação.
        if (response.StatusCode is System.Net.HttpStatusCode.Redirect or System.Net.HttpStatusCode.Moved
            or System.Net.HttpStatusCode.Found or System.Net.HttpStatusCode.TemporaryRedirect
            or System.Net.HttpStatusCode.PermanentRedirect)
        {
            var location = response.Headers.Location;
            if (location is not null)
            {
                var target = location.IsAbsoluteUri ? location : new Uri(request.RequestUri!, location);
                var redirectCheck = SafeUrl.Validate(target.AbsoluteUri);
                if (!redirectCheck.IsValid || !await dnsGuard.IsHostPublicAsync(target.DnsSafeHost, cancellationToken))
                {
                    response.Dispose();
                    throw new InvalidOperationException("Redirecionamento bloqueado pela proteção SSRF.");
                }
            }
        }

        return response;
    }
}