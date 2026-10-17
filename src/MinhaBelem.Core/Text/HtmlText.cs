using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MinhaBelem.Core.Text;

/// <summary>
/// Converte HTML vindo de RSS em texto puro. Remove script/style, descarta tags e
/// decodifica entidades. É a primeira barreira contra XSS armazenado; a segunda é a
/// codificação padrão do Razor na view.
/// </summary>
public static partial class HtmlText
{
    [GeneratedRegex(@"<\s*(script|style)[^>]*>.*?<\s*/\s*\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    public static string ToPlainText(string? html, int maxLength = 1500)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var withoutBlocks = ScriptOrStyle().Replace(html, " ");
        var withoutTags = AnyTag().Replace(withoutBlocks, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags) ?? string.Empty;
        var collapsed = Whitespace().Replace(decoded, " ").Trim();
        return Truncate(collapsed, maxLength);
    }

    public static string Truncate(string? value, int maxLength)
    {
        if (maxLength <= 0) return string.Empty;
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= maxLength) return value;
        var cut = value[..(maxLength - 1)];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > maxLength * 0.6) cut = cut[..lastSpace];
        return cut.TrimEnd(' ', '.', ',', ';', ':') + "…";
    }

    /// <summary>Remove caracteres de controle que quebram layout ou allowlist de URL.</summary>
    public static string Clean(string? value, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsControl(ch) && ch is not ('\n' or '\r' or '\t')) continue;
            if (ch == '\u200B' || ch == '\uFEFF' || ch == '\u00AD') continue;
            sb.Append(ch);
            if (sb.Length >= maxLength) break;
        }
        return Whitespace().Replace(sb.ToString(), " ").Trim();
    }

    /// <summary>Slug estável para URL amigável, sem acentos e sem duplicar o que já existe.</summary>
    public static string Slugify(string? value, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(value)) return "item";
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-');
        }
        var slug = Whitespace().Replace(sb.ToString().Replace("-+", "-", StringComparison.Ordinal), "-").Trim('-');
        if (slug.Length > maxLength) slug = slug[..maxLength].Trim('-');
        return slug.Length == 0 ? "item" : slug;
    }
}
