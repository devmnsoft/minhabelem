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



        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:Default não configurada. Use User Secrets, variável de ambiente ou appsettings do servidor. " +
                "Nunca versione credenciais.");

        services.AddSingleton(_ =>
        {
            var builder = new MySqlDataSourceBuilder(connectionString);
            builder.ConnectionStringBuilder.DefaultCommandTimeout = 30;
            builder.ConnectionStringBuilder.ConnectionTimeout = 15;
            builder.ConnectionStringBuilder.AllowUserVariables = true;
            return builder.Build();
        });

        services.AddSingleton<SchemaMigrator>();
        services.TryAddSingleton<IFeedClient>(sp => new HttpFeedClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient("feed")));

        // Rede: todo cliente externo passa pela proteção SSRF.
        services.AddHttpClient("feed", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(25);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MinhaBelemBot/1.0");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
          .AddHttpMessageHandler<SsrfGuardHandler>();

        services.AddHttpClient("ai", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(60);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MinhaBelemBot/1.0");
        });

        services.AddHttpClient("places", c =>
        {
            c.Timeout = TimeSpan.FromSeconds(20);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MinhaBelemBot/1.0");
        });

        services.AddHttpClient("robots", c => c.Timeout = TimeSpan.FromSeconds(10))
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
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
            .AddClaimsPrincipalFactory<PermissionClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();
        services.AddScoped<IUserStore<ApplicationUser>, MySqlUserStore>();
        services.AddScoped<IRoleStore<IdentityRole<long>>, MySqlRoleStore>();

        services.AddScoped<IdentitySeeder>();

        services.AddHostedService<FeedCollectorWorker>();
        services.AddHostedService<MaintenanceWorker>();

        return services;
    }
}