using Dapper;
using Microsoft.Extensions.DependencyInjection;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
using MinhaBelem.Infrastructure.Discovery;
using MinhaBelem.Web.Resources;
using System.Net;

namespace MinhaBelem.Tests;

[Collection("MySql")]
public sealed class Sprint17Tests(PortalFixture fixture)
{
    [Theory]
    [InlineData("Data: 20 de novembro de 2030. Local: Belém")]
    [InlineData("Data: 20/11/2030. Local: Belém")]
    public void DateWithoutTimeNeverBecomesAConfirmedEveningSession(string text)
    {
        var evidence = new EventStructuredDataExtractor().ExtractEventFromText(text, "https://example.com/event");
        Assert.NotNull(evidence); Assert.True(evidence.Uncertain); Assert.Equal("DateOnly", evidence.Status);
        Assert.Equal(new DateTime(2030,11,20,3,0,0,DateTimeKind.Utc), evidence.StartUtc);
    }
    [MySqlFact]
    public async Task ResearchCatalogIsIdempotentAndDoesNotAuthorizeContinuousCollection()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var sources = scope.ServiceProvider.GetRequiredService<IIngestionRepository>();
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryRepository>();
        var bootstrap = new ResearchedSourceBootstrap(scope.ServiceProvider.GetRequiredService<MySqlConnector.MySqlDataSource>(), sources, discovery);
        await bootstrap.RegisterAsync(default);
        Assert.Equal(0, await bootstrap.RegisterAsync(default));
        var secult = Assert.Single(await sources.SourcesAsync(false, default), x => x.FeedUrl == "https://www.secult.pa.gov.br/");
        Assert.False(secult.Enabled); Assert.False(secult.Approved); Assert.True(secult.RespectRobots);
        Assert.Equal(1440, (await discovery.ProfileAsync(secult.Id, default)).FrequencyMinutes);
    }
    [MySqlFact]
    public async Task CampaignCreationEditingAndDeliveryRespectPermissionsAndPeriod()
    {
        var (admin, _) = await new OperationalTests(fixture).Login();
        using (admin)
        {
            var marker = "Campanha fixture " + Guid.NewGuid().ToString("N");
            var form = new Dictionary<string,string> { ["AdvertiserName"] = marker, ["Headline"] = marker, ["DestinationUrl"] = "https://example.com/advertiser", ["RightsConfirmed"] = "true", ["Active"] = "true" };
            form["DestinationUrl"] = "javascript:alert(1)";
            Assert.Equal(HttpStatusCode.BadRequest, (await PortalFixture.Post(admin, "/admin/anunciantes/campanhas", "/admin/anunciantes", form)).StatusCode);
            form["DestinationUrl"] = "https://example.com/advertiser";
            Assert.Equal(HttpStatusCode.Redirect, (await PortalFixture.Post(admin, "/admin/anunciantes/campanhas", "/admin/anunciantes", form)).StatusCode);
            await using var db = await fixture.OpenAsync();
            var id = await db.ExecuteScalarAsync<long>("SELECT id FROM mb_advertiser_campaigns WHERE advertiser_name=@marker", new { marker });
            using var publicClient = fixture.Client();
            var home = await publicClient.GetStringAsync("/");
            Assert.Contains(marker, home); Assert.Contains("rel=\"sponsored noopener noreferrer\"", home);
            form["Id"] = id.ToString(); form["Headline"] = marker + " editada";
            form["FromDate"] = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd");
            form["ToDate"] = DateTime.UtcNow.AddDays(4).ToString("yyyy-MM-dd");
            Assert.Equal(HttpStatusCode.Redirect, (await PortalFixture.Post(admin, "/admin/anunciantes/campanhas", "/admin/anunciantes", form)).StatusCode);
            Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_advertiser_campaigns WHERE advertiser_name=@marker", new { marker }));
            Assert.DoesNotContain(marker, await publicClient.GetStringAsync("/"));
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/admin/anunciantes/campanhas", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        }
    }
    [MySqlFact]
    public async Task CachedCrawlIsAuditedAndPreservesConditionalRequestValidators()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionRepository>();
        var radar = scope.ServiceProvider.GetRequiredService<IRadarRepository>();
        var source = new IngestionSource { Name = "Cache fixture", FeedUrl = "https://example.com/" + Guid.NewGuid(), Enabled = true, Approved = true, LicenseNote = "Fixture only" };
        source.Id = await ingestion.UpsertSourceAsync(source, default);
        await radar.RecordCrawlHistoryAsync(new() { SourceId = source.Id, Url = source.FeedUrl, StatusCode = 200, ETag = "\"fixture\"", ContentHash = "original-hash" }, default);
        var crawler = new PublicWebCrawler(new CachedClientFactory(), new AllowedRobots(), new HtmlContentExtractor(), new EventStructuredDataExtractor(),
            new SourceChangeDetector(), scope.ServiceProvider.GetRequiredService<IContentDeduplicationService>(), scope.ServiceProvider.GetRequiredService<IDiscoveryRepository>(), radar);
        var result = await crawler.CrawlAsync(source, new() { MaxPagesPerHost = 1 }, default);
        Assert.Empty(result.Errors); Assert.Equal(1, result.UrlsCrawled);
        var latest = await radar.GetLastCrawlForUrlAsync(source.FeedUrl, default);
        Assert.Equal(304, latest!.StatusCode); Assert.Equal("\"fixture\"", latest.ETag); Assert.Equal("original-hash", latest.ContentHash);
    }

    private sealed class CachedClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new CachedHandler());
        private sealed class CachedHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                Assert.Contains("\"fixture\"", request.Headers.GetValues("If-None-Match"));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            }
        }
    }

    [MySqlFact]
    public async Task ContentReportsEnterPrivateModerationAndRejectUnpublishedItems()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var content = scope.ServiceProvider.GetRequiredService<IContentRepository>();
        var item = new ContentItem { Title = "Report fixture", Summary = "Fixture", Slug = Guid.NewGuid().ToString("N"), SourceUrl = "https://example.com/" + Guid.NewGuid(), Status = ContentStatus.Draft };
        var id = await content.InsertAsync(item, null, "Fixture only", default);
        using var client = fixture.Client();
        var fields = new Dictionary<string,string> { ["id"] = id.ToString(), ["motivo"] = "Relato de teste " + Guid.NewGuid(), ["consentimento"] = "true" };
        Assert.Equal(HttpStatusCode.NotFound, (await PortalFixture.Post(client, "/Home/Reportar", "/", fields)).StatusCode);
        item.Status = ContentStatus.Published; item.VerificationStatus = VerificationStatus.Verified;
        Assert.True(await content.UpdateAsync(item, null, "Fixture only", default));
        Assert.Equal(HttpStatusCode.Redirect, (await PortalFixture.Post(client, "/Home/Reportar", "/Home/Detalhe/" + id, fields)).StatusCode);
        await using var db = await fixture.OpenAsync();
        Assert.Equal("Pending", await db.ExecuteScalarAsync<string>("SELECT status FROM mb_community_submissions WHERE message LIKE @message", new { message = "%" + fields["motivo"] }));
        Assert.DoesNotContain(fields["motivo"], await client.GetStringAsync("/Home/Detalhe/" + id));
    }
    [MySqlFact]
    public async Task CommercialInquiryRequiresConsentPersistsAndIsRestrictedToAdministrators()
    {
        using var client = fixture.Client();
        var company = "Fixture " + Guid.NewGuid().ToString("N");
        var form = new Dictionary<string,string> { ["empresa"] = company, ["contato"] = "Teste", ["email"] = "fixture@example.com", ["plano"] = "Guia" };
        var missingConsent = await PortalFixture.Post(client, "/parceiros/anunciar", "/parceiros", form);
        Assert.Equal(HttpStatusCode.Redirect, missingConsent.StatusCode);
        await using var db = await fixture.OpenAsync();
        Assert.Equal(0, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_commercial_inquiries WHERE company=@company", new { company }));
        form["consentimento"] = "true";
        Assert.Equal(HttpStatusCode.Redirect, (await PortalFixture.Post(client, "/parceiros/anunciar", "/parceiros", form)).StatusCode);
        var id = await db.ExecuteScalarAsync<long>("SELECT id FROM mb_commercial_inquiries WHERE company=@company", new { company });
        Assert.True(id > 0);
        Assert.DoesNotContain(company, await client.GetStringAsync("/parceiros"));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/admin/anunciantes")).StatusCode);
        var (admin, _) = await new OperationalTests(fixture).Login();
        using (admin)
        {
            var page = await admin.GetAsync("/admin/anunciantes");
            Assert.Contains(company, await page.Content.ReadAsStringAsync());
            Assert.True(page.Headers.CacheControl!.NoStore);
            var result = await PortalFixture.Post(admin, $"/admin/anunciantes/solicitacoes/{id}/atender", "/admin/anunciantes", new());
            Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
            Assert.True(await db.ExecuteScalarAsync<bool>("SELECT handled FROM mb_commercial_inquiries WHERE id=@id", new { id }));
        }
    }
    [Theory]
    [InlineData("pt-BR")][InlineData("en")][InlineData("fr")][InlineData("es")][InlineData("ru")][InlineData("zh-Hans")][InlineData("ja")]
    public void RadarOperationsAreLocalized(string culture)
    {
        foreach (var key in new[] { "RadarOperations", "RadarNoHeartbeat", "RadarFailures", "RadarCycles", "RadarCycle_interrupted", "RadarSuccessRate", "RadarNoCycles" })
            Assert.NotEqual(key, LocalizationCatalog.Get(key, culture));
    }

    [MySqlFact]
    public async Task EventReversionPreservesChronologyAndLatestEvidence()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionRepository>();
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryRepository>();
        var source = new IngestionSource { Name = "Sprint17 fixture", FeedUrl = "https://example.com/" + Guid.NewGuid(), LicenseNote = "Fixture only" };
        source.Id = await ingestion.UpsertSourceAsync(source, default);
        var original = new DiscoveredItem("Evento fixture", "Resumo", source.FeedUrl + "/event", null, null,
            new(DateTime.UtcNow.AddDays(3), null, "Local A", null, "Scheduled", "0", true, null, null, source.FeedUrl));
        var first = await discovery.StoreAsync(source, original, 0, default);
        var cancelled = original with { Event = original.Event! with { Status = "Cancelled", Location = "Local B" } };
        Assert.True((await discovery.StoreAsync(source, cancelled, 0, default)).Changed);
        Assert.True((await discovery.StoreAsync(source, original, 0, default)).Changed);
        Assert.False((await discovery.StoreAsync(source, original, 0, default)).Changed);
        Assert.Equal("Scheduled", (await discovery.EvidenceAsync(first.Id, default))!.Status);
        await using var db = await fixture.OpenAsync();
        Assert.Equal(3, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_discovery_versions WHERE candidate_id=@Id", new { first.Id }));
        Assert.Equal(0, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_content_items WHERE source_url=@url", new { url = original.Url }));
    }

    [MySqlFact]
    public async Task RadarRecoversInterruptedCycleAndPersistsMetricsWithoutInventingSuccessRate()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var radar = scope.ServiceProvider.GetRequiredService<IRadarRepository>();
        var interrupted = await radar.StartCycleAsync(default);
        var id = await radar.StartCycleAsync(default);
        var cycle = new RadarCycle { Id = id, Status = "partial", SourcesChecked = 4, SourcesFailed = 1, ItemsDiscovered = 3, EventsIdentified = 2, DurationMs = 2500 };
        await radar.FinishCycleAsync(cycle, default);
        await radar.FinishCycleAsync(withStatus(), default);
        RadarCycle withStatus() => new() { Id = id, Status = "failed", SourcesChecked = 0 };
        await radar.UpdateHeartbeatAsync("RadarWorker", "success", "fixture", null, false, default);
        var due = DateTime.UtcNow.AddMinutes(5);
        await radar.ScheduleAsync(due, default);
        Assert.True(Math.Abs(((await radar.GetHeartbeatAsync("RadarWorker", default))!.NextCycleDueAt!.Value - due).TotalSeconds) < 1);
        var stats = await radar.GetDashboardStatsAsync(default);
        Assert.Equal(75m, stats.RecentCycles.Single(x => x.Id == id).SuccessRate);
        Assert.Equal("interrupted", stats.RecentCycles.Single(x => x.Id == interrupted).Status);
        Assert.Null(new RadarCycle().SuccessRate);
        await radar.ScheduleAsync(null, default);
        Assert.Null((await radar.GetHeartbeatAsync("RadarWorker", default))!.NextCycleDueAt);
    }
}
