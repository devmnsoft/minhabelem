using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Infrastructure.Ai;
using MinhaBelem.Infrastructure.Background;
using MinhaBelem.Infrastructure.Feeds;
using MinhaBelem.Infrastructure.Identity;
using MinhaBelem.Infrastructure.MySql;
using MinhaBelem.Infrastructure.Networking;
using MinhaBelem.Infrastructure.Places;
using MinhaBelem.Infrastructure.Discovery;
using MySqlConnector;

namespace Microsoft.Extensions.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMinhaBelemInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DapperTypes.Register();

        services.Configure<CollectorOptions>(configuration.GetSection("Collector"));
        services.Configure<GooglePlacesOptions>(configuration.GetSection("GooglePlaces"));
        services.Configure<AiOptions>(configuration.GetSection("Ai"));



        services.AddSingleton(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("Configure ConnectionStrings:Default nos segredos do servidor.");
            var builder = new MySqlDataSourceBuilder(connectionString);
            builder.ConnectionStringBuilder.DefaultCommandTimeout = 30;
            builder.ConnectionStringBuilder.ConnectionTimeout = 15;
            builder.ConnectionStringBuilder.MaximumPoolSize = Math.Min(builder.ConnectionStringBuilder.MaximumPoolSize, 20);
            builder.ConnectionStringBuilder.DateTimeKind = MySqlDateTimeKind.Utc;
            builder.ConnectionStringBuilder.AllowUserVariables = true;
            builder.UseConnectionOpenedCallback(async (context, ct) =>
            {
                await using var command = context.Connection.CreateCommand();
                command.CommandText = "SET SESSION time_zone = '+00:00'";
                await command.ExecuteNonQueryAsync(ct);
            });
            return builder.Build();
        });

        services.AddSingleton<SchemaMigrator>();
        services.AddSingleton<ApiBudget>();
        services.AddScoped<LegacyDataImporter>();
        services.TryAddSingleton<IFeedClient>(sp => new HttpFeedClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("feed")));

        // Rede: todo cliente externo passa pela proteção SSRF.
        services.AddHttpClient("feed", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(25);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MinhaBelemBot/1.0");
        }).ConfigurePrimaryHttpMessageHandler(PublicHttpTransport.Create)
          .AddHttpMessageHandler<SsrfGuardHandler>();

        services.AddHttpClient("ai", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(60);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MinhaBelemBot/1.0");
        }).ConfigurePrimaryHttpMessageHandler(PublicHttpTransport.Create);

        services.AddHttpClient("places", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MinhaBelemBot/1.0");
        }).ConfigurePrimaryHttpMessageHandler(PublicHttpTransport.Create);

        services.AddHttpClient("robots", c => c.Timeout = TimeSpan.FromSeconds(10))
                .ConfigurePrimaryHttpMessageHandler(PublicHttpTransport.Create)
                .AddHttpMessageHandler<SsrfGuardHandler>();

        services.AddMemoryCache();
        services.AddDataProtection();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<GooglePlacesOptions>>().Value);

        services.AddSingleton<IDnsResolver, SystemDnsResolver>();
        services.AddSingleton<IDnsGuard, DnsGuard>();
        services.AddTransient<SsrfGuardHandler>();
        services.AddScoped<IRobotsGate>(sp => new RobotsGate(sp.GetRequiredService<IHttpClientFactory>().CreateClient("robots"), sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(), sp.GetRequiredService<ILogger<RobotsGate>>()));
        services.AddScoped<IPlacesClient, GooglePlacesClient>();
        services.AddScoped<IFeedReader, FeedReader>();

        services.AddScoped<IContentRepository, ContentRepository>();
        services.AddScoped<IAdministrationRepository, AdministrationRepository>();
        services.AddScoped<IEditorialWorkflow, EditorialWorkflow>();
        services.AddScoped<IDiscoveryRepository, DiscoveryRepository>();
        services.AddScoped<ConnectorHttp>();
        services.AddScoped<IMediaAnalysis, TesseractMediaAnalysis>();
        services.AddScoped<ISourceConnector, RssAtomConnector>();
        services.AddScoped<ISourceConnector, CalendarConnector>();
        services.AddScoped<ISourceConnector, JsonLdConnector>();
        services.AddScoped<ISourceConnector, InstagramConnector>();
        services.AddScoped<ISourceConnector, FacebookConnector>();
        services.AddScoped<ISourceConnector, TikTokConnector>();
        services.AddScoped<ISourceConnector, YouTubeConnector>();
        services.AddScoped<ISourceConnector, WebDiscoveryConnector>();
        services.AddScoped<ISourceConnector, GovernmentDataConnector>();
        services.AddScoped<ISourceConnector, CustomJsonConnector>();
        services.AddScoped<ISourceConnector, GooglePlacesConnector>();
        services.AddScoped<IIngestionRepository, IngestionRepository>();
        services.AddScoped<IPlaceRepository, PlaceRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IAiRunRepository, AiRunRepository>();
        services.AddScoped<ICommunityRepository, CommunityRepository>();
        services.AddScoped<IAdvertiserRepository, AdvertiserRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AiOptions>>().Value);
        services.AddScoped<AiRunLogger>();
        services.AddSingleton<IAiClientResolver, AiClientResolver>();
        services.AddScoped<IAiAnalysisService, AiAnalysisService>();

        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<long>>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<PermissionClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();
        services.AddScoped<IUserStore<ApplicationUser>, MySqlUserStore>();
        services.AddScoped<IRoleStore<IdentityRole<long>>, MySqlRoleStore>();

        services.AddScoped<IdentitySeeder>();

        services.AddSingleton<FeedCollectorWorker>();
        services.AddSingleton<ISourceCollection>(sp => sp.GetRequiredService<FeedCollectorWorker>());
        services.AddHostedService(sp => sp.GetRequiredService<FeedCollectorWorker>());
        services.AddHostedService<MaintenanceWorker>();

        return services;
    }
}
