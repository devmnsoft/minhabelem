using System.Diagnostics;
using System.Net;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Text;

namespace MinhaBelem.Infrastructure.Discovery;

public sealed class PublicWebCrawler(
    IHttpClientFactory httpClientFactory,
    IRobotsGate robotsGate,
    IHtmlContentExtractor htmlExtractor,
    IEventStructuredDataExtractor eventExtractor,
    ISourceChangeDetector changeDetector,
    IContentDeduplicationService deduplicationService,
    IDiscoveryRepository discoveryRepo,
    IRadarRepository radarRepo) : IPublicWebCrawler
{
    public async Task<CrawlResult> CrawlAsync(IngestionSource source, CrawlerOptions? options, CancellationToken ct)
    {
        return await CrawlInternalAsync(source.FeedUrl, source, options ?? new CrawlerOptions(), ct);
    }

    public async Task<CrawlResult> CrawlTargetUrlAsync(string targetUrl, CrawlerOptions? options, CancellationToken ct)
    {
        var dummySource = new IngestionSource
        {
            Id = 0,
            Name = "Varredura Manual Radar",
            FeedUrl = targetUrl,
            Protocol = FeedProtocol.Html,
            Approved = true,
            Enabled = true,
            LicenseNote = "Varredura autorizada para descoberta pública"
        };

        return await CrawlInternalAsync(targetUrl, dummySource, options ?? new CrawlerOptions(), ct);
    }

    private async Task<CrawlResult> CrawlInternalAsync(string startUrl, IngestionSource source, CrawlerOptions options, CancellationToken ct)
    {
        var result = new CrawlResult();
        var sw = Stopwatch.StartNew();

        var queue = new Queue<(string Url, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var consecutiveErrors = 0;

        queue.Enqueue((startUrl, 0));
        var client = httpClientFactory.CreateClient("feed");

        while (queue.Count > 0 && visited.Count < options.MaxPagesPerHost)
        {
            ct.ThrowIfCancellationRequested();

            var (currentUrl, depth) = queue.Dequeue();
            if (!visited.Add(currentUrl)) continue;

            // 1. SSRF Guard
            var validation = SafeUrl.Validate(currentUrl);
            if (!validation.IsValid || validation.Uri is null)
            {
                result.Errors.Add($"URL bloqueada por segurança (SSRF): {currentUrl}");
                continue;
            }

            // 2. Circuit Breaker
            if (consecutiveErrors >= 5)
            {
                result.Errors.Add($"Circuit breaker acionado após {consecutiveErrors} falhas consecutivas em {validation.Uri.Host}.");
                break;
            }

            // 3. robots.txt compliance
            if (source.RespectRobots)
            {
                var robotsDecision = await robotsGate.IsAllowedAsync(validation.Uri, ct);
                if (!robotsDecision.Allowed)
                {
                    result.Errors.Add($"URL desautorizada pelo robots.txt: {currentUrl} ({robotsDecision.Reason})");
                    continue;
                }
            }

            var requestSw = Stopwatch.StartNew();
            string? html = null;
            var statusCode = 0;
            string? etag = null;
            DateTime? lastModified = null;
            string? errorMessage = null;

            try
            {
                if (source.Id > 0)
                {
                    var profile = await discoveryRepo.ProfileAsync(source.Id, ct);
                    if (!await discoveryRepo.ReserveRequestAsync(source.Id, Math.Clamp(profile.DailyRequestLimit, 1, 1000), ct))
                    {
                        result.Errors.Add("Limite diário de consultas atingido.");
                        break;
                    }
                }
                // Polite delay between requests
                if (visited.Count > 1 && options.DelayBetweenRequestsMs > 0)
                {
                    await Task.Delay(options.DelayBetweenRequestsMs, ct);
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, validation.Uri);
                request.Headers.Add("User-Agent", options.UserAgent);

                var lastCrawl = await radarRepo.GetLastCrawlForUrlAsync(currentUrl, ct);
                if (!string.IsNullOrWhiteSpace(lastCrawl?.ETag))
                {
                    request.Headers.TryAddWithoutValidation("If-None-Match", lastCrawl.ETag);
                }
                if (lastCrawl?.LastModified is { } lm)
                {
                    request.Headers.IfModifiedSince = new DateTimeOffset(DateTime.SpecifyKind(lm, DateTimeKind.Utc));
                }

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                statusCode = (int)response.StatusCode;
                etag = response.Headers.ETag?.ToString();
                lastModified = response.Content.Headers.LastModified?.UtcDateTime;

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    // 304 Não modificado
                    consecutiveErrors = 0;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    consecutiveErrors++;
                    errorMessage = $"HTTP {(int)response.StatusCode}";
                    result.Errors.Add($"Erro ao acessar {currentUrl}: {errorMessage}");
                }
                else
                {
                    consecutiveErrors = 0;
                    if (response.Content.Headers.ContentLength > options.MaxContentLengthBytes)
                    {
                        errorMessage = "Resposta excede limite de tamanho.";
                        result.Errors.Add(errorMessage);
                    }
                    else
                    {
                        await using var stream = await response.Content.ReadAsStreamAsync(ct);
                        using var reader = new StreamReader(stream);
                        var buffer = new char[options.MaxContentLengthBytes + 1];
                        var readCount = await reader.ReadBlockAsync(buffer.AsMemory(), ct);
                        if (readCount > options.MaxContentLengthBytes)
                        {
                            errorMessage = "Conteúdo excedeu limite de 2MB.";
                        }
                        else
                        {
                            html = new string(buffer, 0, readCount);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                consecutiveErrors++;
                errorMessage = "Falha na requisição: " + ex.GetType().Name;
                result.Errors.Add(errorMessage);
            }
            finally
            {
                requestSw.Stop();
                result.UrlsCrawled++;
            }

            if (string.IsNullOrWhiteSpace(html))
            {
                // Registra histórico de falha/304
                await radarRepo.RecordCrawlHistoryAsync(new CrawlHistoryEntry
                {
                    SourceId = source.Id > 0 ? source.Id : null,
                    Url = currentUrl,
                    UrlHash = Hashing.UrlHash(currentUrl),
                    StatusCode = statusCode,
                    ETag = etag,
                    LastModified = lastModified,
                    DurationMs = (int)requestSw.ElapsedMilliseconds,
                    ErrorMessage = errorMessage
                }, ct);
                continue;
            }

            // 4. Detecção de Alteração
            var contentHash = changeDetector.ComputeContentHash(html);

            // 5. Extração de Conteúdo e Links
            var extractedContent = htmlExtractor.Extract(html, validation.Uri);

            // 6. Extração de Eventos Estruturados
            var discoveredEvents = eventExtractor.ExtractEvents(html, currentUrl);
            var eventsIdentifiedCount = discoveredEvents.Count;

            // 7. Salva itens na fila de candidatos do MinhaBelém
            var itemsStored = 0;
            if (discoveredEvents.Count > 0)
            {
                foreach (var evItem in discoveredEvents)
                {
                    var dup = await deduplicationService.DetectDuplicateAsync(evItem.Title, evItem.Summary, evItem.Url, evItem.Event?.StartUtc, ct);
                    if (source.Id > 0)
                    {
                        var stored = await discoveryRepo.StoreAsync(source, evItem, 0, ct);
                        if (stored.Changed) itemsStored++;
                    }
                    result.EventsIdentified++;
                }
            }
            else if (extractedContent.IsBelemRelevant && !string.IsNullOrWhiteSpace(extractedContent.Title))
            {
                var newsItem = new DiscoveredItem(
                    Title: extractedContent.Title,
                    Summary: extractedContent.Summary,
                    Url: extractedContent.CanonicalUrl ?? currentUrl,
                    Author: extractedContent.Author,
                    PostedAt: extractedContent.PublishedAt
                )
                {
                    MediaUrl = extractedContent.LeadImageUrl,
                    MediaCredit = extractedContent.LeadImageCredit
                };

                var dup = await deduplicationService.DetectDuplicateAsync(newsItem.Title, newsItem.Summary, newsItem.Url, null, ct);
                if (source.Id > 0)
                {
                    var stored = await discoveryRepo.StoreAsync(source, newsItem, 0, ct);
                    if (stored.Changed) itemsStored++;
                }
                result.ItemsDiscovered++;
            }

            // 8. Registro de Histórico de Coleta
            await radarRepo.RecordCrawlHistoryAsync(new CrawlHistoryEntry
            {
                SourceId = source.Id > 0 ? source.Id : null,
                Url = currentUrl,
                UrlHash = Hashing.UrlHash(currentUrl),
                StatusCode = statusCode,
                ETag = etag,
                LastModified = lastModified,
                ContentHash = contentHash,
                DiscoveredItemsCount = itemsStored,
                EventsIdentifiedCount = eventsIdentifiedCount,
                Depth = depth,
                DurationMs = (int)requestSw.ElapsedMilliseconds,
                ErrorMessage = errorMessage
            }, ct);

            // 9. Descoberta de Novas Fontes Relevantes
            if (depth < options.MaxDepth)
            {
                foreach (var link in extractedContent.OutboundLinks)
                {
                    if (Uri.TryCreate(link, UriKind.Absolute, out var linkUri))
                    {
                        if (string.Equals(linkUri.Host, validation.Uri.Host, StringComparison.OrdinalIgnoreCase))
                        {
                            // Link interno: segue rastreando
                            queue.Enqueue((link, depth + 1));
                        }
                        else
                        {
                            // Link externo: avalia como fonte candidata para catálogo
                            var domain = linkUri.GetLeftPart(UriPartial.Authority);
                            var isLikelyBelemSource = extractedContent.IsBelemRelevant ||
                                linkUri.Host.Contains("belem", StringComparison.OrdinalIgnoreCase) ||
                                linkUri.Host.Contains("para.gov", StringComparison.OrdinalIgnoreCase) ||
                                linkUri.Host.Contains("pa.gov", StringComparison.OrdinalIgnoreCase);

                            if (isLikelyBelemSource)
                            {
                                var sourceCandidate = new DiscoveredSource
                                {
                                    SourceName = linkUri.Host,
                                    Url = domain,
                                    UrlHash = Hashing.UrlHash(domain),
                                    Category = "cultura",
                                    Municipality = "Belém",
                                    DiscoveredFromUrl = currentUrl,
                                    EvaluationStatus = SourceEvaluationStatus.Pending,
                                    FeedProtocolCandidate = "html",
                                    ConfidenceScore = 0.85m,
                                    EvaluationNotes = $"Fonte descoberta a partir de link na página {currentUrl}"
                                };

                                var newId = await radarRepo.InsertDiscoveredSourceAsync(sourceCandidate, ct);
                                if (newId > 0)
                                {
                                    result.DiscoveredSourcesCount++;
                                }
                            }
                        }
                    }
                }
            }
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }
}
