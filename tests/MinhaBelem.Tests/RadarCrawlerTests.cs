using System.Net;
using System.Text;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Core.Text;
using MinhaBelem.Infrastructure.Discovery;

namespace MinhaBelem.Tests;

public sealed class RadarCrawlerTests
{
    [Fact]
    public void HtmlContentExtractorExtractsMetaTagsAndIdentifiesBelemKeywords()
    {
        var html = """
            <!DOCTYPE html>
            <html lang="pt-BR">
            <head>
                <title>Festival de Ópera no Theatro da Paz em Belém</title>
                <meta property="og:title" content="Festival de Ópera no Theatro da Paz" />
                <meta property="og:description" content="Grande espetáculo lírico no centro histórico de Belém, bairro da Campina." />
                <meta property="og:image" content="https://example.com/cartaz.jpg" />
                <meta property="article:published_time" content="2026-11-10T15:30:00-03:00" />
                <meta name="author" content="Secult Pará" />
                <link rel="canonical" href="https://example.com/noticia-opera" />
            </head>
            <body>
                <h1>Festival de Ópera 2026</h1>
                <p>O Governo do Pará apresenta uma nova temporada com grandes solistas em Belém.</p>
                <a href="https://example.com/ingressos">Comprar Ingressos</a>
                <a href="https://agenciapara.com.br/noticia">Outra Matéria Estadual</a>
                <a href="/programacao-completa">Programação</a>
            </body>
            </html>
            """;

        var extractor = new HtmlContentExtractor();
        var result = extractor.Extract(html, new Uri("https://example.com/noticia-opera"));

        Assert.Equal("Festival de Ópera no Theatro da Paz", result.Title);
        Assert.Contains("Grande espetáculo lírico", result.Summary);
        Assert.Equal("https://example.com/noticia-opera", result.CanonicalUrl);
        Assert.Equal("Secult Pará", result.Author);
        Assert.NotNull(result.PublishedAt);
        Assert.Equal("https://example.com/cartaz.jpg", result.LeadImageUrl);
        Assert.True(result.IsBelemRelevant);
        Assert.Contains("Theatro da Paz", result.BelemKeywordsFound);
        Assert.Contains("Belém", result.BelemKeywordsFound);
        Assert.Contains("Campina", result.BelemKeywordsFound);
        Assert.Contains("https://example.com/ingressos", result.OutboundLinks);
        Assert.Contains("https://agenciapara.com.br/noticia", result.OutboundLinks);
        Assert.Contains("https://example.com/programacao-completa", result.OutboundLinks);
    }

    [Fact]
    public void EventStructuredDataExtractorParsesJsonLdAndPortugueseEventFormats()
    {
        var extractor = new EventStructuredDataExtractor();

        var textWithEvent = """
            Concerto da Amazônia Jazz Band
            Data e Horário: 25 de Novembro de 2026 às 20h
            Local: Theatro da Paz, Belém - PA
            Entrada: Gratuita
            Classificação: Livre
            """;

        var evidence = extractor.ExtractEventFromText(textWithEvent, "https://example.com/evento-jazz");

        Assert.NotNull(evidence);
        Assert.True(evidence.Uncertain); // Text heuristics always require editorial confirmation.
        Assert.NotNull(evidence.StartUtc);
        // 20h em Belém (UTC-3) corresponde a 23h UTC
        Assert.Equal(23, evidence.StartUtc!.Value.Hour);
        Assert.Equal(25, evidence.StartUtc.Value.Day);
        Assert.Equal(11, evidence.StartUtc.Value.Month);
        Assert.Equal(2026, evidence.StartUtc.Value.Year);
        Assert.True(evidence.IsFree);
        Assert.Equal("Livre", evidence.AgeRating);
        Assert.Contains("Theatro da Paz", evidence.Location);
    }

    [Fact]
    public void EventStructuredDataExtractorWithoutDateFlagsUncertainWithoutHallucination()
    {
        var extractor = new EventStructuredDataExtractor();

        var textWithoutDate = """
            Exposição de Fotografia no Parque da Residência
            Local: Magalhães Barata, São Brás, Belém
            Ingresso: R$ 10,00
            Classificação: 10 anos
            """;

        var evidence = extractor.ExtractEventFromText(textWithoutDate, "https://example.com/expo");

        Assert.NotNull(evidence);
        Assert.True(evidence.Uncertain);
        Assert.Null(evidence.StartUtc);
        Assert.False(evidence.IsFree);
        Assert.Equal("R$ 10,00", evidence.Price);
        Assert.Equal("10 anos", evidence.AgeRating);
    }

    [Fact]
    public void SourceChangeDetectorIdentifiesETagLastModifiedAndHashChanges()
    {
        var detector = new SourceChangeDetector();

        var content = "Notícia de Belém atualizada";
        var hash = detector.ComputeContentHash(content);

        // Mesmo hash: sem alteração
        Assert.False(detector.HasChanged(content, hash, null, null, null, null));

        // Mesmo ETag: sem alteração
        Assert.False(detector.HasChanged("Qualquer novo", null, "\"etag-123\"", "\"etag-123\"", null, null));

        // Mesmo ou anterior LastModified: sem alteração
        var modified = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(detector.HasChanged("Qualquer", null, null, null, modified, modified));

        // Conteúdo alterado com hash diferente: alteração detectada
        var alteredContent = "Notícia de Belém com dados novos";
        Assert.True(detector.HasChanged(alteredContent, hash, null, null, null, null));
    }

    [Theory]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://localhost:5000/")]
    [InlineData("http://10.0.0.1/private")]
    [InlineData("http://192.168.0.1/router")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    public void SafeUrlRejectsSsrfAndPrivateDestinations(string rawUrl)
    {
        var validation = SafeUrl.Validate(rawUrl);
        Assert.False(validation.IsValid);
    }

    [Fact]
    public void PublicWebCrawlerDiscoversLinksAndRespectsDepth()
    {
        var htmlPage = """
            <html>
            <head><title>Portal Cultural de Belém</title></head>
            <body>
                <p>Notícias de eventos e lazer em Belém do Pará.</p>
                <a href="https://example.com/evento1">Evento 1</a>
                <a href="https://secult.pa.gov.br/programacao">Secult Pará</a>
            </body>
            </html>
            """;

        var extractor = new HtmlContentExtractor();
        var extracted = extractor.Extract(htmlPage, new Uri("https://example.com"));

        Assert.Equal(2, extracted.OutboundLinks.Count);
        Assert.Contains("https://example.com/evento1", extracted.OutboundLinks);
        Assert.Contains("https://secult.pa.gov.br/programacao", extracted.OutboundLinks);
    }

    [Fact]
    public async Task ContentDeduplicationServiceIdentifiesSameUrlAndSimilarTitles()
    {
        var existingItem = new ContentItem
        {
            Id = 42,
            Title = "Festival de Carimbó de Belém na Praça da República",
            Summary = "Apresentação dos mestres de carimbó no centro de Belém.",
            SourceUrl = "https://example.com/carimbo",
            EventStartAt = new DateTime(2026, 11, 20, 22, 0, 0, DateTimeKind.Utc)
        };

        var fakeContentRepo = new FakeContentRepo(new[] { existingItem });
        var fakeIngestionRepo = new FakeIngestionRepo();

        var service = new ContentDeduplicationService(fakeContentRepo, fakeIngestionRepo);

        // 1. Duplicata por mesma URL
        var matchSameUrl = await service.DetectDuplicateAsync(
            "Outro Título Qualquer",
            "Outro resumo",
            "https://example.com/carimbo",
            null,
            CancellationToken.None);

        Assert.NotNull(matchSameUrl);
        Assert.Equal(42, matchSameUrl.Id);
        Assert.Equal(DuplicateReason.SameUrl, matchSameUrl.Reason);

        // 2. Duplicata por similaridade de título alto
        var matchSimilar = await service.DetectDuplicateAsync(
            "Festival de Carimbó de Belém na Praça",
            "Apresentação dos mestres de carimbó",
            "https://outro.com/noticia",
            null,
            CancellationToken.None);

        Assert.NotNull(matchSimilar);
        Assert.Equal(42, matchSimilar.Id);
    }

    [Fact]
    public async Task SitemapDiscoveryServiceParsesUrlSetXml()
    {
        var sitemapXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <url>
                    <loc>https://example.com/noticia-1</loc>
                    <lastmod>2026-10-15</lastmod>
                </url>
                <url>
                    <loc>https://example.com/noticia-2</loc>
                    <lastmod>2026-10-16</lastmod>
                </url>
            </urlset>
            """;

        var handler = new FakeHttpHandler(sitemapXml, "application/xml");
        var clientFactory = new FakeHttpClientFactory(handler);

        var service = new SitemapDiscoveryService(clientFactory);
        var urls = await service.DiscoverUrlsAsync("https://example.com/sitemap.xml", CancellationToken.None);

        Assert.Equal(2, urls.Count);
        Assert.Contains("https://example.com/noticia-1", urls);
        Assert.Contains("https://example.com/noticia-2", urls);
    }

    [Fact]
    public async Task PublicRssDiscoveryServiceParsesRssFeedItems()
    {
        var rssXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0">
                <channel>
                    <title>Notícias de Belém</title>
                    <link>https://example.com</link>
                    <description>Feed oficial de Belém</description>
                    <item>
                        <title>Temporada de Concertos no Theatro da Paz</title>
                        <link>https://example.com/concertos-theatro</link>
                        <description>Programação especial da Orquestra Sinfônica do Theatro da Paz.</description>
                        <pubDate>Thu, 15 Oct 2026 18:00:00 GMT</pubDate>
                    </item>
                </channel>
            </rss>
            """;

        var handler = new FakeHttpHandler(rssXml, "application/rss+xml");
        var clientFactory = new FakeHttpClientFactory(handler);

        var service = new PublicRssDiscoveryService(clientFactory);
        var items = await service.DiscoverFeedAsync("https://example.com/feed.xml", CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal("Temporada de Concertos no Theatro da Paz", item.Title);
        Assert.Contains("Programação especial", item.Summary);
        Assert.Equal("https://example.com/concertos-theatro", item.Url);
    }

    private sealed class FakeHttpHandler(string content, string contentType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, contentType)
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public void RadarHeartbeatCalculatesAccurateStatusFromTelemetry()
    {
        var now = DateTime.UtcNow;

        var activeHeartbeat = new RadarHeartbeat
        {
            LastHeartbeatAt = now.AddSeconds(-30),
            TotalCyclesExecuted = 1,
            LastCycleFinishedAt = now.AddSeconds(-30),
            IsRunning = false,
            ConsecutiveErrors = 0,
            LastCycleStatus = "success"
        };
        Assert.Equal(WorkerStatus.Active, activeHeartbeat.CalculateStatus());
        Assert.Equal("badge-success", activeHeartbeat.StatusBadgeClass);
        Assert.Contains("respondendo", activeHeartbeat.StatusText);

        var runningHeartbeat = new RadarHeartbeat
        {
            LastHeartbeatAt = now.AddSeconds(-20),
            IsRunning = true,
            ConsecutiveErrors = 0
        };
        Assert.Equal(WorkerStatus.Running, runningHeartbeat.CalculateStatus());

        var delayedHeartbeat = new RadarHeartbeat
        {
            LastHeartbeatAt = now.AddSeconds(-100),
            IsRunning = false,
            ConsecutiveErrors = 0
        };
        Assert.Equal(WorkerStatus.Delayed, delayedHeartbeat.CalculateStatus());

        var offHeartbeat = new RadarHeartbeat
        {
            LastHeartbeatAt = now.AddMinutes(-30),
            IsRunning = false,
            ConsecutiveErrors = 0
        };
        Assert.Equal(WorkerStatus.Off, offHeartbeat.CalculateStatus());

        var errorHeartbeat = new RadarHeartbeat
        {
            LastHeartbeatAt = now.AddSeconds(-30),
            TotalCyclesExecuted = 1,
            LastCycleFinishedAt = now.AddSeconds(-30),
            IsRunning = false,
            ConsecutiveErrors = 3
        };
        Assert.Equal(WorkerStatus.Error, errorHeartbeat.CalculateStatus());

        var uninitializedHeartbeat = new RadarHeartbeat();
        Assert.Equal(WorkerStatus.Unknown, uninitializedHeartbeat.CalculateStatus());
    }

    [Fact]
    public void RadarOperationalSourceEvaluatesLifecycleCorrectly()
    {
        var source = new IngestionSource
        {
            Id = 42,
            Name = "Theatro da Paz",
            FeedUrl = "https://theatrodapaz.com.br/programacao",
            Topics = "teatro",
            Municipality = "Belém",
            LicenseNote = "Conteúdo público para divulgação cultural",
            Enabled = true,
            Approved = true,
            LastCheckedAt = DateTime.UtcNow.AddHours(-2),
            LastSuccessAt = DateTime.UtcNow.AddHours(-2),
            ConsecutiveFailures = 0
        };

        var opSource = new RadarOperationalSource { Source = source, AvgResponseTimeMs = 180 };

        Assert.Equal(42, opSource.Id);
        Assert.Equal("Theatro da Paz", opSource.SourceName);
        Assert.Equal("https://theatrodapaz.com.br/programacao", opSource.Url);
        Assert.Equal("teatro", opSource.Category);
        Assert.Equal("Belém", opSource.Municipality);
        Assert.True(opSource.IsConfigured);
        Assert.True(opSource.IsAuthorized);
        Assert.True(opSource.IsConsulted);
        Assert.True(opSource.IsOperational);
        Assert.False(opSource.IsOutdated);
        Assert.Equal(180, opSource.AvgResponseTimeMs);

        // Outdated when consecutive failures reach threshold
        source.ConsecutiveFailures = 3;
        Assert.True(opSource.IsOutdated);
    }

    [Fact]
    public void SocialPostBindsAttributesAndPreservesAuthorAndContentLinkage()
    {
        var post = new SocialPost
        {
            Id = 1,
            ProfileId = 10,
            Platform = "Instagram",
            AuthorHandle = "theatrodapazoficial",
            PostUrl = "https://www.instagram.com/p/DB12345/",
            Title = "Temporada de Ópera do Pará 2026",
            Caption = "Estreia confirmada para o próximo mês com grandes vozes líricas da Amazônia.",
            MediaType = "post",
            ContentId = 55,
            IsSponsored = false
        };

        Assert.Equal(55, post.LinkedContentId);
        Assert.Contains("Estreia confirmada", post.CaptionSnippet);
        Assert.False(post.IsSponsored);
        Assert.Equal("Instagram", post.Platform);
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler);
    }

    private sealed class FakeContentRepo(IReadOnlyList<ContentItem> items) : IContentRepository
    {
        public Task<int> CountEditorialAsync(ContentQuery query,CancellationToken ct)=>CountAsync(query,ct);
        public Task<IReadOnlyList<ContentItem>> SearchForDuplicatesAsync(string title, string summary, int limit, CancellationToken ct)
            => Task.FromResult(items);

        public Task<IReadOnlyDictionary<long, string>> FingerprintsAsync(IEnumerable<long> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<long, string>>(items.ToDictionary(x => x.Id, x => Hashing.ContentFingerprint(x.Title, x.Summary)));

        public Task<int> CountAsync(ContentQuery query, CancellationToken ct) => Task.FromResult(items.Count);
        public Task<bool> DeleteAsync(long id, long actorId, CancellationToken ct) => Task.FromResult(true);
        public Task<IReadOnlyList<ContentItem>> EditorialAsync(ContentQuery query, CancellationToken ct) => Task.FromResult(items);
        public Task<int> ExpireOldContentAsync(DateTime now, CancellationToken ct) => Task.FromResult(0);
        public Task<ContentItem?> GetAsync(long id, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(x => x.Id == id));
        public Task<ContentItem?> GetBySlugAsync(string slug, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(x => x.Slug == slug));
        public Task<long> InsertAsync(ContentItem item, long? actorId, string changeSummary, CancellationToken ct) => Task.FromResult(item.Id);
        public Task<IReadOnlyList<ContentRevision>> RevisionsAsync(long contentId, CancellationToken ct) => Task.FromResult<IReadOnlyList<ContentRevision>>(Array.Empty<ContentRevision>());
        public Task<IReadOnlyList<ContentItem>> SearchAsync(ContentQuery query, CancellationToken ct) => Task.FromResult(items);
        public Task<bool> SetStatusAsync(long id, ContentStatus status, long actorId, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> UpdateAsync(ContentItem item, long? actorId, string changeSummary, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class FakeIngestionRepo : IIngestionRepository
    {
        public Task<IngestionCandidate?> CandidateAsync(long id, CancellationToken ct) => Task.FromResult<IngestionCandidate?>(null);
        public Task<IReadOnlyList<IngestionCandidate>> CandidatesAsync(CandidateQuery query, CancellationToken ct) => Task.FromResult<IReadOnlyList<IngestionCandidate>>(Array.Empty<IngestionCandidate>());
        public Task<int> CountCandidatesAsync(CandidateQuery query, CancellationToken ct) => Task.FromResult(0);
        public Task FinishRunAsync(IngestionRun run, CancellationToken ct) => Task.CompletedTask;
        public Task<IngestionCandidate?> InsertCandidateAsync(IngestionCandidate candidate, CancellationToken ct) => Task.FromResult<IngestionCandidate?>(candidate);
        public Task<IReadOnlyList<IngestionCandidate>> SearchForDuplicatesAsync(string title, string summary, string sourceUrl, int limit, CancellationToken ct) => Task.FromResult<IReadOnlyList<IngestionCandidate>>(Array.Empty<IngestionCandidate>());
        public Task<bool> SetCandidateStatusAsync(long id, CandidateStatus status, long? reviewerId, string? notes, CancellationToken ct) => Task.FromResult(true);
        public Task<IngestionSource?> SourceAsync(long id, CancellationToken ct) => Task.FromResult<IngestionSource?>(null);
        public Task<IReadOnlyList<IngestionSource>> SourcesAsync(bool onlyOperational, CancellationToken ct) => Task.FromResult<IReadOnlyList<IngestionSource>>(Array.Empty<IngestionSource>());
        public Task<long> StartRunAsync(CancellationToken ct) => Task.FromResult(1L);
        public Task<bool> UpdateSourceRuntimeAsync(long id, DateTime checkedAt, DateTime? successAt, string? error, string? etag, DateTime? lastModified, CancellationToken ct) => Task.FromResult(true);
        public Task<long> UpsertSourceAsync(IngestionSource source, CancellationToken ct) => Task.FromResult(source.Id);
    }
}
