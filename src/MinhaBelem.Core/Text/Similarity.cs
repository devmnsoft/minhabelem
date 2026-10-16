namespace MinhaBelem.Core.Text;

/// <summary>Comparação de similaridade entre textos, sem dependência externa.</summary>
public static class Similarity
{
    /// <summary>
    /// Coeficiente de Jaccard sobre tokens significativos (0..1).
    /// Igual a 1.0 significa conjuntos de tokens idênticos.
    /// </summary>
    public static double Jaccard(string? a, string? b)
    {
        var left = TextNormalizer.SignificantTokens(a).ToHashSet(StringComparer.Ordinal);
        var right = TextNormalizer.SignificantTokens(b).ToHashSet(StringComparer.Ordinal);
        if (left.Count == 0 && right.Count == 0) return 1;
        if (left.Count == 0 || right.Count == 0) return 0;
        var intersection = left.Count;
        foreach (var token in left) if (right.Contains(token)) intersection--;
        var union = left.Count + right.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    /// <summary>
    /// Distância de Levenshtein limitada (abaixo de <paramref name="maxDistance"/> o cálculo é interrompido).
    /// Usada como segunda barreira contra duplicidade com título ligeramente diferente.
    /// </summary>
    public static int Levenshtein(string? a, string? b, int maxDistance = 32)
    {
        a ??= string.Empty;
        b ??= string.Empty;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        if (Math.Abs(a.Length - b.Length) > maxDistance) return maxDistance + 1;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = char.ToLowerInvariant(a[i - 1]) == char.ToLowerInvariant(b[j - 1]) ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                if (current[j] < rowMin) rowMin = current[j];
            }
            if (rowMin > maxDistance) return maxDistance + 1;
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    /// <summary>Normaliza a distância de edição em razão do maior título, dentro de 0..1.</summary>
    public static double NormalizedEditDistance(string? a, string? b)
    {
        var left = TextNormalizer.Normalize(a);
        var right = TextNormalizer.Normalize(b);
        var max = Math.Max(left.Length, right.Length);
        if (max == 0) return 0;
        return (double)Levenshtein(left, right, max) / max;
    }
}