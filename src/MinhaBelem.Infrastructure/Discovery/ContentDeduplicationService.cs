using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Infrastructure.Discovery;

public sealed class ContentDeduplicationService(
    IContentRepository contentRepo,
    IIngestionRepository ingestionRepo) : IContentDeduplicationService
{
    private readonly DuplicateDetector _detector = new(titleThreshold: 0.65);

    public async Task<DuplicateMatch?> DetectDuplicateAsync(string title, string summary, string url, DateTime? eventStart, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        // 1. Busca duplicidades no acervo já publicado
        var publishedDuplicates = await contentRepo.SearchForDuplicatesAsync(title, summary, 10, ct);
        if (publishedDuplicates.Count > 0)
        {
            var existingItems = publishedDuplicates.Select(p => (p.Id, p.Title, p.Summary, p.SourceUrl)).ToList();
            var fingerprints = await contentRepo.FingerprintsAsync(publishedDuplicates.Select(p => p.Id), ct);

            var bestPublished = _detector.FindBest(title, summary, url, existingItems, fingerprints);
            if (bestPublished is not null) return bestPublished;

            // Verificação espaço-temporal para eventos futuros
            if (eventStart.HasValue)
            {
                foreach (var ev in publishedDuplicates.Where(x => x.EventStartAt.HasValue))
                {
                    var timeDiff = (ev.EventStartAt!.Value - eventStart.Value).Duration();
                    if (timeDiff <= TimeSpan.FromHours(4))
                    {
                        var sim = Similarity.Jaccard(title, ev.Title);
                        if (sim >= 0.50)
                        {
                            return new DuplicateMatch(ev.Id, ev.Title, 0.90, DuplicateReason.SimilarTitle);
                        }
                    }
                }
            }
        }

        // 2. Busca duplicidades na fila de ingestão/candidatos
        var candidateDuplicates = await ingestionRepo.SearchForDuplicatesAsync(title, summary, url, 10, ct);
        if (candidateDuplicates.Count > 0)
        {
            var existingCandidates = candidateDuplicates.Select(c => (c.Id, c.Title, c.Summary, c.SourceUrl)).ToList();
            var candidateFingerprints = candidateDuplicates.ToDictionary(c => c.Id, c => Hashing.ContentFingerprint(c.Title, c.Summary));

            var bestCandidate = _detector.FindBest(title, summary, url, existingCandidates, candidateFingerprints);
            if (bestCandidate is not null) return bestCandidate;
        }

        return null;
    }
}
