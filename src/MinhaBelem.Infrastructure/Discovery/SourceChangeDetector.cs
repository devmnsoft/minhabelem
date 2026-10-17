using System.Security.Cryptography;
using System.Text;
using MinhaBelem.Core.Abstractions;

namespace MinhaBelem.Infrastructure.Discovery;

public sealed class SourceChangeDetector : ISourceChangeDetector
{
    public bool HasChanged(
        string newContent,
        string? previousContentHash,
        string? newEtag,
        string? previousEtag,
        DateTime? newModified,
        DateTime? previousModified)
    {
        // 1. Verificação por ETag HTTP
        if (!string.IsNullOrWhiteSpace(newEtag) && !string.IsNullOrWhiteSpace(previousEtag) &&
            string.Equals(newEtag.Trim('"', ' '), previousEtag.Trim('"', ' '), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 2. Verificação por Last-Modified HTTP
        if (newModified.HasValue && previousModified.HasValue && newModified.Value <= previousModified.Value)
        {
            return false;
        }

        // 3. Verificação por Content Hash (SHA256)
        if (!string.IsNullOrWhiteSpace(previousContentHash))
        {
            var newHash = ComputeContentHash(newContent);
            if (string.Equals(newHash, previousContentHash, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public string ComputeContentHash(string content)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        var bytes = Encoding.UTF8.GetBytes(normalized);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
