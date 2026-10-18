using System.Net;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
using MinhaBelem.Infrastructure.Background;
using MinhaBelem.Web.Resources;
using MySqlConnector;

namespace MinhaBelem.Tests;

[Collection("MySql")]
public sealed class Sprint14Tests(PortalFixture fixture)
{
    [Fact]
    public void WebDoesNotRegisterWorkersOrExecutionInterfaces()
    {
        var services = new ServiceCollection();
        services.AddMinhaBelemInfrastructure(new ConfigurationBuilder().Build(), registerWorkers: false);
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(IHostedService) && s.ImplementationType?.Namespace == typeof(RadarWorker).Namespace);
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(ISourceCollection));
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(IRadarWorker));
        Assert.Contains(services, s => s.ServiceType == typeof(ICollectionQueue));
    }

    [Theory]
    [InlineData("pt-BR")][InlineData("en")][InlineData("fr")][InlineData("es")][InlineData("ru")][InlineData("zh-Hans")][InlineData("ja")]
    public void NewComponentsHaveTranslations(string culture)
    {
        foreach (var key in new[] { "Hero_Title", "Hero_Subtitle", "QueueTitle", "Queue_pending", "EmptyTitle", "WeeklyAgenda", "NavNeighborhood", "CinemaTheater", "Category_cultura" })
            Assert.NotEqual(key, LocalizationCatalog.Get(key, culture));
    }

    [Fact]
    public void StandaloneWorkerHostBuildsWithoutWebAuthenticationServices()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddMinhaBelemInfrastructure(builder.Configuration);
        using var provider = builder.Services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.Contains(builder.Services, s => s.ServiceType == typeof(ISourceCollection));
    }

    [MySqlFact]
    public async Task MainPublicAndAdministrativePagesRenderAcrossSevenLanguages()
    {
        var (client, _) = await new OperationalTests(fixture).Login();
        using (client)
        {
            foreach (var culture in SupportedCultures.All)
            {
                client.DefaultRequestHeaders.AcceptLanguage.Clear();
                client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
                foreach (var path in new[] {
                    "/", "/?busca=Belém", "/agenda", "/agenda/semana", "/agora", "/cinema-teatro", "/programacao-infantil",
                    "/guia", "/explore", "/explore/glossario", "/mobilidade", "/meu-bairro", "/parceiros", "/conta/login", "/conta/perfil", "/conta/sessoes", "/privacidade", "/termos",
                    "/admin", "/admin/radar", "/admin/evolucao", "/admin/conteudo", "/admin/fontes", "/admin/usuarios", "/admin/perfis", "/admin/integracoes", "/admin/anunciantes", "/admin/mobilidade", "/admin/bairros", "/admin/idiomas", "/admin/traducoes", "/admin/auditoria", "/admin/relatorios", "/admin/configuracoes", "/admin/colaboracoes", "/admin/lugares", "/editorial"
                })
                {
                    var response = await client.GetAsync(path);
                    var html = await response.Content.ReadAsStringAsync();
                    Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect, $"{culture} {path}: {response.StatusCode} " + (response.IsSuccessStatusCode ? "" : html.Split("HEADERS")[0]));
                    Assert.DoesNotContain("Unable to resolve service", html);
                    Assert.DoesNotContain("Hero_Title", html);
                    Assert.DoesNotContain("QueueTitle", html);
                    if (Environment.GetEnvironmentVariable("MINHABELEM_VISUAL_SNAPSHOTS") is { Length: > 0 } output && response.StatusCode == HttpStatusCode.OK)
                    {
                        var directory = Path.Combine(output, culture);
                        Directory.CreateDirectory(directory);
                        var name = path == "/" ? "home" : path.Trim('/').Replace('/', '-').Replace('?', '-').Replace('=', '-');
                        html = System.Text.RegularExpressions.Regex.Replace(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", "");
                        await File.WriteAllTextAsync(Path.Combine(directory, name + ".html"), html);
                    }
                }
            }
        }
    }

    [MySqlFact]
    public async Task ManualHttpRequestOnlyEnqueuesAndServiceProcessesPersistedRequest()
    {
        var (client, _) = await new OperationalTests(fixture).Login();
        using (client)
        {
            await using var db = await fixture.OpenAsync();
            await db.ExecuteAsync("DELETE FROM collection_requests");
            var runsBefore = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_ingestion_runs");
            var response = await PortalFixture.Post(client, "/admin/fontes/executar", "/admin/fontes", new());
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(runsBefore, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_ingestion_runs"));
            var request = await db.QuerySingleAsync<CollectionRequest>("SELECT id, status, attempts FROM collection_requests");
            Assert.Equal("pending", request.Status);
            var collector = new RecordingCollector();
            await using var provider = QueueProvider(collector);
            var worker = new CollectionQueueWorker(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<MySqlDataSource>(), NullLogger<CollectionQueueWorker>.Instance);
            Assert.True(await worker.ProcessNextAsync(default));
            Assert.Equal(1, collector.Calls);
            Assert.Equal("completed", await db.ExecuteScalarAsync<string>("SELECT status FROM collection_requests WHERE id=@Id", request));
        }
    }

    [MySqlFact]
    public async Task QueueRecoversInterruptedRequestsAndStopsAfterThreeFailures()
    {
        await using var db = await fixture.OpenAsync();
        await db.ExecuteAsync("DELETE FROM collection_requests");
        await db.ExecuteAsync("INSERT INTO collection_requests (kind,requested_by_id,status,attempts) VALUES ('feeds',1,'running',1)");
        await using var provider = QueueProvider(new RecordingCollector { Fail = true });
        var worker = new CollectionQueueWorker(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<MySqlDataSource>(), NullLogger<CollectionQueueWorker>.Instance);
        Assert.True(await worker.ProcessNextAsync(default));
        Assert.Equal("pending", await db.ExecuteScalarAsync<string>("SELECT status FROM collection_requests"));
        await db.ExecuteAsync("UPDATE collection_requests SET available_at=UTC_TIMESTAMP(6)");
        Assert.True(await worker.ProcessNextAsync(default));
        var request = await db.QuerySingleAsync<CollectionRequest>("SELECT status, attempts, error FROM collection_requests");
        Assert.Equal("failed", request.Status);
        Assert.Equal(3, request.Attempts);
        Assert.DoesNotContain("secret-value", request.Error);
        Assert.False(await worker.ProcessNextAsync(default));
    }

    [MySqlFact]
    public async Task QueueLockExcludesAnotherConsumer()
    {
        await using var db = await fixture.OpenAsync();
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT GET_LOCK('minhabelem_collection_queue',0)"));
        try
        {
            await using var provider = QueueProvider(new RecordingCollector());
            var worker = new CollectionQueueWorker(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<MySqlDataSource>(), NullLogger<CollectionQueueWorker>.Instance);
            Assert.False(await worker.ProcessNextAsync(default));
        }
        finally { await db.ExecuteAsync("SELECT RELEASE_LOCK('minhabelem_collection_queue')"); }
    }

    [MySqlFact]
    public async Task DetailPagesRenderWithExplicitDisposableFixtures()
    {
        var (client, actor) = await new OperationalTests(fixture).Login();
        using (client)
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var content = scope.ServiceProvider.GetRequiredService<IContentRepository>();
            var places = scope.ServiceProvider.GetRequiredService<IPlaceRepository>();
            var itemId = await content.InsertAsync(new ContentItem { Title = "Teste automatizado — banco descartável", Summary = "Fixture de revisão visual; não é uma notícia real.", Body = "Conteúdo exclusivo de teste.", Slug = Guid.NewGuid().ToString("N"), SourceName = "Fixture", SourceUrl = "https://example.com/" + Guid.NewGuid().ToString("N"), Status = ContentStatus.Published, VerificationStatus = VerificationStatus.Verified }, actor, "Fixture", default);
            var placeId = await places.UpsertAsync(new Place { Name = "Lugar de teste descartável", GooglePlaceId = "fixture_" + Guid.NewGuid().ToString("N"), PrimaryType = "restaurant", IsPublished = true }, default);
            try
            {
                var paths = new Dictionary<string, string> { ["news-detail"] = $"/Home/Detalhe/{itemId}", ["guide-detail"] = $"/guia/{placeId}", ["editorial-edit"] = $"/editorial/editar/{itemId}", ["account-denied"] = "/conta/acesso-negado" };
                var neighborhoods = await scope.ServiceProvider.GetRequiredService<INeighborhoodRepository>().GetNeighborhoodsAsync(default);
                if (neighborhoods.FirstOrDefault() is { } neighborhood) paths["neighborhood-detail"] = "/meu-bairro/" + neighborhood.Slug;
                var itineraries = await scope.ServiceProvider.GetRequiredService<ITouristRepository>().GetItinerariesAsync(default);
                if (itineraries.FirstOrDefault() is { } itinerary) paths["itinerary-detail"] = "/explore/roteiro/" + itinerary.Slug;
                foreach (var culture in SupportedCultures.All)
                {
                    client.DefaultRequestHeaders.AcceptLanguage.Clear(); client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
                    foreach (var (name, url) in paths)
                    {
                        var response = await client.GetAsync(url);
                        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Forbidden, $"{culture} {name}: {response.StatusCode}");
                        if (Environment.GetEnvironmentVariable("MINHABELEM_VISUAL_SNAPSHOTS") is { Length: > 0 } output)
                        {
                            var html = await response.Content.ReadAsStringAsync();
                            html = System.Text.RegularExpressions.Regex.Replace(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", "");
                            Directory.CreateDirectory(Path.Combine(output, culture));
                            await File.WriteAllTextAsync(Path.Combine(output, culture, name + ".html"), html);
                        }
                    }
                }
            }
            finally
            {
                await content.DeleteAsync(itemId, actor, default);
                await using var db = await fixture.OpenAsync();
                await db.ExecuteAsync("DELETE FROM mb_places WHERE id=@placeId", new { placeId });
            }
        }
    }

    [Fact]
    public void StaleRunningHeartbeatDoesNotClaimWorkerIsRunning()
        => Assert.Equal(WorkerStatus.Off, new RadarHeartbeat { LastHeartbeatAt = DateTime.UtcNow.AddMinutes(-4), IsRunning = true }.Status);

    [Fact]
    public void HeartbeatWithoutCompletedCycleDoesNotClaimRadarIsActive()
        => Assert.Equal(WorkerStatus.Unknown, new RadarHeartbeat { LastHeartbeatAt = DateTime.UtcNow }.Status);

    [MySqlFact]
    public async Task CrawlerDiscoveryWithoutFeedRunUsesNullForeignKey()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionRepository>();
        var source = new IngestionSource { Name = "Fonte fixture Radar", FeedUrl = "https://example.com/" + Guid.NewGuid().ToString("N"), Protocol = FeedProtocol.Html, LicenseNote = "Fixture" };
        source.Id = await ingestion.UpsertSourceAsync(source, default);
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryRepository>();
        var stored = await discovery.StoreAsync(source, new DiscoveredItem("Teste Radar", "Persistência sem execução de feed", source.FeedUrl, null, DateTime.UtcNow), 0, default);
        await using var db = await fixture.OpenAsync();
        Assert.Null(await db.ExecuteScalarAsync<long?>("SELECT run_id FROM mb_ingestion_candidates WHERE id=@Id", new { stored.Id }));
    }

    private ServiceProvider QueueProvider(ISourceCollection collector)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new MySqlDataSourceBuilder(fixture.ConnectionString).Build());
        services.AddSingleton(collector);
        return services.BuildServiceProvider();
    }

    private sealed class RecordingCollector : ISourceCollection
    {
        public int Calls { get; private set; }
        public bool Fail { get; init; }
        public Task CollectOnceAsync(CancellationToken ct)
        {
            Calls++;
            return Fail ? Task.FromException(new HttpRequestException("secret-value")) : Task.CompletedTask;
        }
    }
}
