using System.Globalization;
using System.Text;

namespace MinhaBelem.Core.Text;

/// <summary>
/// Normalização de texto para comparação de títulos. Usada na detecção de duplicidade
/// e na deduplicação de candidatos vindos de fontes diferentes.
/// </summary>
public static class TextNormalizer
{
    /// <summary>Remove acentos, baixa caixa e colapsa pontuação/espaços. Máxima word tokens: 40.</summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var decomposed = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(ch);
        }
        var ascii = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        var outSb = new StringBuilder(ascii.Length);
        var lastSpace = true;
        foreach (var ch in ascii)
        {
            if (char.IsLetterOrDigit(ch))
            {
                outSb.Append(ch);
                lastSpace = false;
            }
            else if (!lastSpace)
            {
                outSb.Append(' ');
                lastSpace = true;
            }
        }
        return outSb.ToString().Trim();
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a","o","as","os","um","uma","uns","umas","de","do","da","dos","das","em","no","na","nos","nas",
        "por","pelo","pela","para","com","sem","e","ou","mas","que","se","ao","aos","a","as","e","é",
        "ha","vai","ser","foi","sao","esta","estao","apos","ate","sobre","entre","como","quando","onde",
        "the","of","and","to","in","for","on","with","at","by","from"
    };

    /// <summary>Tokens significativos usados na comparação de similaridade.</summary>
    public static IReadOnlyList<string> Tokens(string? input) =>
        Normalize(input)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3 && !StopWords.Contains(t))
            .ToArray();

    /// <summary>Remove nomes próprios comuns deiobém/Pará para não viabilizar a similaridade.</summary>
    public static IReadOnlyList<string> SignificantTokens(string? input) =>
        Tokens(input)
            .Where(t => !BelemGeography.LocalityTokens.Contains(t))
            .ToArray();
}