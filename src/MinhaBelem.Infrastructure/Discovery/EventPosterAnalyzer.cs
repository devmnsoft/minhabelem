using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Services;

namespace MinhaBelem.Infrastructure.Discovery;

public sealed class EventPosterAnalyzer(
    IMediaAnalysis mediaAnalysis,
    IEventStructuredDataExtractor eventExtractor,
    IAiAnalysisService aiAnalysis) : IEventPosterAnalyzer
{
    public async Task<EventEvidence?> AnalyzePosterAsync(string imageUrl, string contextTitle, CancellationToken ct)
    {
        var validation = SafeUrl.Validate(imageUrl);
        if (!validation.IsValid || validation.Uri is null) return null;

        string? extractedText = null;

        // Tenta OCR se estiver configurado
        if (mediaAnalysis.IsConfigured)
        {
            try
            {
                var result = await mediaAnalysis.AnalyzeAsync(0, 0, ct);
                if (!string.IsNullOrWhiteSpace(result.Text))
                {
                    extractedText = result.Text;
                }
            }
            catch
            {
                // OCR não disponível ou falhou; prossegue com análise de texto/metadados
            }
        }

        // Se OCR retornou texto, extrai dados estruturados dele
        if (!string.IsNullOrWhiteSpace(extractedText))
        {
            var eventFromOcr = eventExtractor.ExtractEventFromText(extractedText, imageUrl);
            if (eventFromOcr is not null) return eventFromOcr;
        }

        // Se não conseguiu pelo OCR mas tem contexto/título, tenta análise assistida por IA
        var aiInput = new AiAnalysisInput(contextTitle, extractedText ?? "Cartaz autorizado de divulgação em Belém", imageUrl, Array.Empty<Core.Entities.ContentItem>());
        var aiResult = await aiAnalysis.AnalyzeAsync(aiInput, ct);

        return aiResult.Event;
    }
}
