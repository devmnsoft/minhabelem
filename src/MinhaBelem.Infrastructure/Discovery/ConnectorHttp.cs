using System.Net;
using System.Net.Http.Headers;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Security;

namespace MinhaBelem.Infrastructure.Discovery;

public sealed class ConnectorHttp(IHttpClientFactory clients, IConfiguration configuration)
{
    public string? Token(SourceProfile profile) => string.IsNullOrWhiteSpace(profile.CredentialReference) ? null : configuration[$"Sources:Credentials:{profile.CredentialReference}:Token"] ?? configuration[$"Sources:Credentials:{profile.CredentialReference}:ApiKey"];
    public bool HasCredential(SourceProfile profile) => !string.IsNullOrWhiteSpace(Token(profile));
    public string? CredentialIssue(SourceContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Profile.CredentialReference)) return null;
        var host = configuration[$"Sources:Credentials:{context.Profile.CredentialReference}:AllowedHost"];
        return !HasCredential(context.Profile) || !string.Equals(host, new Uri(context.Source.FeedUrl).Host, StringComparison.OrdinalIgnoreCase)
            ? "Credencial ausente ou não autorizada para o domínio desta fonte." : null;
    }
    public async Task<(string? Body, string? ETag, DateTime? Modified)> ReadAsync(SourceContext context, CancellationToken ct, string? host = null, bool post = false)
    {
        var valid = SafeUrl.Validate(context.Source.FeedUrl);
        if (!valid.IsValid || valid.Uri is null || (host is not null && !string.Equals(host, valid.Uri.Host, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Endpoint não autorizado para este conector.");
        if (CredentialIssue(context) is { } credentialIssue) throw new InvalidOperationException(credentialIssue);
        // Credenciais nunca devem fazer parte da URL cadastrada (nem de logs HTTP).
        if (System.Text.RegularExpressions.Regex.IsMatch(Uri.UnescapeDataString(valid.Uri.Query), @"(?:^|[?&])(key|token|access_token|api_key|password)=", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) throw new InvalidOperationException("Configure credenciais por referência segura, fora da URL.");
        using var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get, valid.Uri);
        if (HasCredential(context.Profile))
        {
            var apiKey = configuration[$"Sources:Credentials:{context.Profile.CredentialReference}:ApiKey"];
            if (apiKey is not null)
            {
                if (valid.Uri.Host is not ("customsearch.googleapis.com" or "www.googleapis.com")) throw new InvalidOperationException("Chave Google não autorizada para este domínio.");
                request.Headers.Add("X-Goog-Api-Key", apiKey);
            }
            else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(context.Profile));
        }
        if (!string.IsNullOrWhiteSpace(context.Source.ETag)) request.Headers.TryAddWithoutValidation("If-None-Match", context.Source.ETag);
        if (context.Source.LastModified is { } modified) request.Headers.IfModifiedSince = new DateTimeOffset(DateTime.SpecifyKind(modified, DateTimeKind.Utc));
        if (post) request.Content = System.Net.Http.Json.JsonContent.Create(new { max_count = Math.Clamp(context.Profile.MaxItems, 1, 20), cursor = long.TryParse(context.Profile.Checkpoint, out var cursor) ? cursor : 0L });
        using var response = await clients.CreateClient("feed").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.NotModified) return (null, context.Source.ETag, context.Source.LastModified);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Conector respondeu HTTP " + (int)response.StatusCode, null, response.StatusCode);
        if (response.Content.Headers.ContentLength is > 2_000_000) throw new InvalidOperationException("Resposta excede o limite de tamanho.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var buffer = new char[2_000_001];
        var count = await reader.ReadBlockAsync(buffer.AsMemory(), ct);
        if (count > 2_000_000) throw new InvalidOperationException("Resposta excede o limite de tamanho.");
        return (new string(buffer, 0, count), response.Headers.ETag?.ToString(), response.Content.Headers.LastModified?.UtcDateTime);
    }
}
