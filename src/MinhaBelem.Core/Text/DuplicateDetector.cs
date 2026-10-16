namespace MinhaBelem.Core.Text;

public sealed record DuplicateMatch(long Id, string Title, double Score, DuplicateReason Reason);

public enum DuplicateReason { SameUrl, SameContent, SimilarTitle }

/// <summary>
/// Detecção de duplicidade entre candidatos e conteúdo já publicado.
/// Combina três sinais: URL idêntica, impressão digital de conteúdo e similaridade de título.
/// </summary>
public sealed class DuplicateDetector
{
    private readonly double _titleThreshold;

    public DuplicateDetector(double titleThreshold = 0.62) => _titleThreshold = titleThreshold;

    public DuplicateMatch? FindBest(
        string title,
        string? summary,
        string? sourceUrl,
        IReadOnlyList<(long Id, string Title, string Summary, string SourceUrl)> existing,
        IReadOnlyDictionary<long, string> fingerprintsById)
    {
        if (existing.Count == 0) return null;
        var urlHash = sourceUrl is null ? null : Hashing.UrlHashHex(sourceUrl);
        var fingerprint = Hashing.ContentFingerprint(title, summary);

        DuplicateMatch? best = null;
        foreach (var (id, otherTitle, otherSummary, otherUrl) in existing)
        {
            double score;
            DuplicateReason reason;

            if (urlHash is not null && Hashing.UrlHashHex(otherUrl) == urlHash)
            {
                score = 1;
                reason = DuplicateReason.SameUrl;
            }
            else if (fingerprintsById.TryGetValue(id, out var otherFingerprint) && otherFingerprint == fingerprint)
            {
                score = 0.99;
                reason = DuplicateReason.SameContent;
            }
            else
            {
                var jaccard = Similarity.Jaccard(title, otherTitle);
                var edit = 1 - Similarity.NormalizedEditDistance(title, otherTitle);
                score = Math.Max(jaccard, edit * 0.95);
                reason = DuplicateReason.SimilarTitle;
            }

            if (score < _titleThreshold) continue;
            if (best is null || score > best.Score) best = new DuplicateMatch(id, otherTitle, score, reason);
        }

        return best;
    }
}