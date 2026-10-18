using System.Net;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
using MinhaBelem.Infrastructure.Ai;
using MinhaBelem.Infrastructure.Background;
using MinhaBelem.Infrastructure.MySql;
using MySqlConnector;

namespace MinhaBelem.Tests;

public sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MINHABELEM_TEST_MYSQL"))) Skip = "Defina MINHABELEM_TEST_MYSQL para um servidor MySQL descartável."; }
}
[CollectionDefinition("MySql", DisableParallelization = true)]
public sealed class MySqlCollection : ICollectionFixture<PortalFixture> { }

public sealed class PortalFixture : IAsyncLifetime
{
    private readonly string database = "minhabelem_test_" + Guid.NewGuid().ToString("N");
    private string? server;
    public string ConnectionString { get; private set; } = "";
    public PortalFactory Factory { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        server = Environment.GetEnvironmentVariable("MINHABELEM_TEST_MYSQL");
        if (string.IsNullOrEmpty(server)) return;
        var cs = new MySqlConnectionStringBuilder(server);
        if (cs.Server is not ("127.0.0.1" or "localhost") || !string.IsNullOrEmpty(cs.Database)) throw new InvalidOperationException("Os testes exigem MySQL local descartável, sem Database na conexão.");
        await using var root = new MySqlConnection(server);
        await root.OpenAsync();
        await root.ExecuteAsync($"CREATE DATABASE `{database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        cs.Database = database; cs.AllowUserVariables = true; cs.DateTimeKind = MySqlDateTimeKind.Utc;
        ConnectionString = cs.ConnectionString;
        await using var data = new MySqlDataSourceBuilder(ConnectionString).Build();
        await new SchemaMigrator(data, NullLogger<SchemaMigrator>.Instance).ApplyAsync();
        Factory = new PortalFactory(ConnectionString);
    }
    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        if (string.IsNullOrEmpty(server)) return;
        MySqlConnection.ClearAllPools();
        await using var root = new MySqlConnection(server);
        await root.OpenAsync();
        // Somente a base aleatoria criada por esta fixture pode ser removida.
        if (!Regex.IsMatch(database, "^minhabelem_test_[a-f0-9]{32}$")) throw new InvalidOperationException();
        await root.ExecuteAsync($"DROP DATABASE `{database}`");
    }
    private static int clientId;
    public HttpClient Client()
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Test-Client", Interlocked.Increment(ref clientId).ToString());
        return client;
    }
    public async Task<MySqlConnection> OpenAsync() { var db = new MySqlConnection(ConnectionString); await db.OpenAsync(); return db; }
    public static string Token(string html)
    {
        var m1 = Regex.Match(html, @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");
        if (m1.Success) return WebUtility.HtmlDecode(m1.Groups[1].Value);
        var m2 = Regex.Match(html, @"value=""([^""]+)""[^>]*name=""__RequestVerificationToken""");
        if (m2.Success) return WebUtility.HtmlDecode(m2.Groups[1].Value);
        return string.Empty;
    }
    public static async Task<HttpResponseMessage> Post(HttpClient client, string path, string formPage, Dictionary<string, string> fields)
    {
        var html = await client.GetStringAsync(formPage);
        var token = Token(html);
        Assert.NotEmpty(token);
        fields["__RequestVerificationToken"] = token;
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }
}

public sealed class PortalFactory(string connection) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging=>logging.SetMinimumLevel(LogLevel.Warning));
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connection,
            ["Collector:Enabled"] = "false",
            ["GoogleRoutes:Enabled"] = "false",
            ["Maps:BrowserKey"] = null
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IFeedClient>(); services.AddSingleton<IFeedClient, SampleFeed>();
            services.RemoveAll<IRobotsGate>(); services.AddScoped<IRobotsGate, AllowedRobots>();
            services.RemoveAll<IAiClientResolver>(); services.AddSingleton<IAiClientResolver, SampleAi>();
            services.RemoveAll<IPlacesClient>(); services.AddScoped<IPlacesClient, SamplePlaces>();
            services.Configure<AiOptions>(o => { o.Enabled = true; o.Groq.ApiKey = "test-double"; });
            // Limites continuam habilitados em producao; nos testes de fluxo nao contam as requisicoes da suite inteira.
            services.Configure<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>(o => o.GlobalLimiter = null);
            services.AddSingleton<IStartupFilter, TestClientAddress>();
        });
    }
}
public sealed class TestClientAddress : IStartupFilter
{
    public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next) => app =>
    {
        app.Use(async (ctx, continuation) => { if (int.TryParse(ctx.Request.Headers["X-Test-Client"], out var id)) ctx.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2." + (id % 250 + 1)); await continuation(); });
        next(app);
    };
}
public sealed class AllowedRobots : IRobotsGate { public Task<RobotsDecision> IsAllowedAsync(Uri target, CancellationToken ct) => Task.FromResult(new RobotsDecision(true, null)); }
public sealed class SampleFeed : IFeedClient
{
    public Task<FeedReadResult> ReadAsync(Uri uri, string? etag, DateTime? modified, CancellationToken ct) => Task.FromResult(new FeedReadResult(new[] { new FeedItem("Festival de cultura em Belém " + uri.AbsolutePath, "Programação confirmada pela organização em Belém do Pará.", uri.AbsoluteUri.TrimEnd('/') + "/evento", "Organização", DateTime.UtcNow, null) }, "test-etag", DateTime.UtcNow));
}
public sealed class SampleAi : IAiClientResolver
{
    public IAiCompletionClient Resolve(AiOptions options) => new Client();
    private sealed class Client : IAiCompletionClient
    {
        public AiProvider Provider => AiProvider.Groq; public bool IsConfigured => true; public string Model => "fixture";
        public Task<AiCompletion> CompleteAsync(string system, string user, CancellationToken ct) => Task.FromResult(new AiCompletion("""{"categoria":"cultura","assunto":"Teatro","municipio":"Belém","bairro":"Nazaré","publicos":[],"gratuito":true,"tem_data":false,"ofensivo":false,"sensivel":false,"denuncia":false,"alerta_critico":false,"desatualizado":false,"motivos":[]}""", 10, 10, 0, "fixture-v1"));
    }
}
public sealed class SamplePlaces : IPlacesClient
{
    public bool IsConfigured => true;
    public Task<IReadOnlyList<Place>> SearchAsync(string type, double lat, double lng, double radius, int max, CancellationToken ct) => Task.FromResult<IReadOnlyList<Place>>(Array.Empty<Place>());
    public Task<Place?> GetDetailsAsync(string id, CancellationToken ct) => Task.FromResult<Place?>(new Place { GooglePlaceId = id, Name = "Local fornecido pela API simulada", FormattedAddress = "Belém, PA", PhotoAuthors = Array.Empty<PhotoAuthor>() });
    public Task<string?> GetPhotoUriAsync(string? name, CancellationToken ct) => Task.FromResult<string?>(null);
}
