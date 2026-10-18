using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using Microsoft.Extensions.Localization;
using MinhaBelem.Core.Security;
using MinhaBelem.Infrastructure.Identity;
using MinhaBelem.Infrastructure.MySql;
using MinhaBelem.Web.Resources;
using MinhaBelem.Web.Security;

var builder = WebApplication.CreateBuilder(args);
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");

builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.AddSingleton<IStringLocalizerFactory, MinhaBelemStringLocalizerFactory>();
builder.Services.AddTransient(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
builder.Services.AddSingleton<IStringLocalizer, MinhaBelemStringLocalizer>();
builder.Services.AddSingleton<IStringLocalizer<SharedResource>, MinhaBelemStringLocalizer>();

builder.Services.AddControllersWithViews(o => { o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()); o.ModelBinderProviders.Insert(0,new MinhaBelem.Web.Models.DecimalInputBinderProvider()); })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(o => {
        o.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource));
    });

builder.Services.AddMinhaBelemInfrastructure(builder.Configuration, registerWorkers: false);
builder.Services.AddScoped<LocalCredentialRecovery>();
var keyProtection = builder.Services.AddDataProtection().SetApplicationName("MinhaBelem");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    keyProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    if (builder.Configuration["DataProtection:CertificatePath"] is { Length: > 0 } certificatePath)
        keyProtection.ProtectKeysWithCertificate(new System.Security.Cryptography.X509Certificates.X509Certificate2(certificatePath, builder.Configuration["DataProtection:CertificatePassword"]));
    else if (OperatingSystem.IsWindows()) keyProtection.ProtectKeysWithDpapi();
}
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
        if (System.Net.IPAddress.TryParse(proxy, out var address)) o.KnownProxies.Add(address);
});
builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    o.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    o.DefaultSignInScheme = IdentityConstants.ExternalScheme;
}).AddIdentityCookies();
builder.Services.Configure<IdentityOptions>(o =>
{
    o.Password.RequiredLength = 12; o.User.RequireUniqueEmail = true;
    o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
});
builder.Services.AddScoped<PortalCookieEvents>();
builder.Services.AddScoped<PortalTwoFactorCookieEvents>();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "__Host-MinhaBelem.Editor"; o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Strict;
    o.LoginPath = "/conta/login"; o.AccessDeniedPath = "/conta/acesso-negado";
    o.ExpireTimeSpan = TimeSpan.FromHours(2); o.SlidingExpiration = false; o.EventsType = typeof(PortalCookieEvents);
});
builder.Services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme, o =>
{
    o.Cookie.Name = "__Host-MinhaBelem.Mfa"; o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict; o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    o.Cookie.HttpOnly = true; o.Cookie.Path = "/"; o.EventsType = typeof(PortalTwoFactorCookieEvents);
});
builder.Services.AddAntiforgery(o => { o.Cookie.Name = "__Host-MinhaBelem.Csrf"; o.Cookie.SecurePolicy = CookieSecurePolicy.Always; });
builder.Services.AddAuthorization(o =>
{
    foreach (var permission in Permissions.All)
        o.AddPolicy(permission, p => p.RequireAuthenticatedUser().RequireClaim("amr", "mfa").RequireClaim(PermissionClaimsPrincipalFactory.PermissionClaimType, permission));
});
builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    void Add(string name, int limit) => o.AddPolicy(name, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    Add("public", 120); Add("login", 12); Add("submission", 3);
    Add("routing",10); Add("assistant",5); Add("itinerary",2);
});

var app = builder.Build();
app.UseForwardedHeaders();
if (args.Contains("--audit-operations"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Diagnóstico exclusivo de Development local.");
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await MinhaBelem.Infrastructure.Background.OperationsDiagnostics.ReadLocalAsync(app.Services.GetRequiredService<MySqlConnector.MySqlDataSource>(),builder.Configuration,environment:app.Environment.EnvironmentName),new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    return;
}
if (args.Contains("--audit-auth"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Diagnóstico exclusivo de Development local.");
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await AuthenticationDiagnostics.ReadLocalAsync(app.Services.GetRequiredService<MySqlConnector.MySqlDataSource>(), environment: app.Environment.EnvironmentName), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return;
}
var stageAgenda=args.Contains("--stage-agenda");
if (Array.IndexOf(args, stageAgenda?"--stage-agenda":"--import-agenda") is var agendaArgument && agendaArgument >= 0)
{
    if(stageAgenda&&args.Contains("--import-agenda"))throw new InvalidOperationException("Escolha revisão ou importação publicada, nunca os dois modos.");
    var connection = new MySqlConnector.MySqlConnectionStringBuilder(builder.Configuration.GetConnectionString("Default") ?? "");
    if (!app.Environment.IsDevelopment() || connection.Server is not ("localhost" or "127.0.0.1") || agendaArgument + 1 >= args.Length)
        throw new InvalidOperationException("Importação explícita exclusiva do banco local em Development.");
    var manifest = CuratedAgendaImporter.Parse(await File.ReadAllTextAsync(args[agendaArgument + 1]));
    using var scope = app.Services.CreateScope();
    var importer = new CuratedAgendaImporter(scope.ServiceProvider.GetRequiredService<MinhaBelem.Core.Abstractions.IContentRepository>());
    var result = await importer.ImportAsync(manifest, default, stageForReview:stageAgenda);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
    return;
}
if (args.Contains("--register-researched-sources"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Comando exclusivo de Development local.");
    using var scope = app.Services.CreateScope();
    var count = await scope.ServiceProvider.GetRequiredService<MinhaBelem.Infrastructure.Discovery.ResearchedSourceBootstrap>().RegisterAsync(default);
    Console.WriteLine("Fontes pesquisadas cadastradas, inativas e aguardando autorização: " + count);
    return;
}

if (args.Contains("--bootstrap-homologation"))
{
    if (!app.Environment.IsDevelopment() || !OperatingSystem.IsWindows())
        throw new InvalidOperationException("Bootstrap exclusivo do computador Windows local em Development.");
    using var scope = app.Services.CreateScope();
    var report = await scope.ServiceProvider.GetRequiredService<HomologationBootstrap>().RunAsync();
    Console.WriteLine("Relatório local restrito (validade 24h): " + report);
    return;
}

if (args.Contains("--recover-homologation-passwords"))
{
    using var scope = app.Services.CreateScope();
    var accountIndex = Array.IndexOf(args, "--account");
    if (accountIndex >= 0 && (accountIndex+1 >= args.Length || args[accountIndex+1].StartsWith("--",StringComparison.Ordinal)))
        throw new InvalidOperationException("Informe a conta de homologação após --account.");
    string[]? accounts = accountIndex >= 0 && accountIndex+1 < args.Length ? [args[accountIndex+1]] : null;
    var report = await scope.ServiceProvider.GetRequiredService<LocalCredentialRecovery>().RunAsync(args.Contains("--confirm-local-homologation"),accounts);
    Console.WriteLine("Relatório local restrito, validade 24h: " + report);
    return;
}

// Operacao explicita. Nunca aplicar SQL no startup ou publish normal.
if (args.Contains("--initialize-database"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<SchemaMigrator>().ApplyAsync();
    return;
}
if (args.Contains("--bootstrap-admin"))
{
    using var scope = app.Services.CreateScope();
    var email = builder.Configuration["Bootstrap:Email"] ?? throw new InvalidOperationException("Configure Bootstrap:Email em segredo do servidor.");
    var password = builder.Configuration["Bootstrap:Password"] ?? throw new InvalidOperationException("Configure Bootstrap:Password em segredo do servidor.");
    await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync(new(email, password, "Administrador inicial"));
    return;
}
if (args.Contains("--import-legacy"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<LegacyDataImporter>().ImportAsync();
    return;
}

if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }

var supportedCultures = SupportedCultures.All.Select(c => new CultureInfo(c)).ToList();
var locOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(SupportedCultures.Portuguese),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};
locOptions.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider { CookieName = ".AspNetCore.Culture" });
app.UseRequestLocalization(locOptions);

// Intercepta rotas com prefixo de cultura e ajusta a cultura na requisicao
app.Use(async (ctx, next) =>
{
    var segments = ctx.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (segments is { Length: > 0 } && SupportedCultures.All.Contains(segments[0], StringComparer.OrdinalIgnoreCase))
    {
        var cult = SupportedCultures.Normalize(segments[0]);
        var ci = new CultureInfo(cult);
        CultureInfo.CurrentCulture = ci;
        CultureInfo.CurrentUICulture = ci;
        var preference=CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(ci));
        if(ctx.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName]!=preference)
            ctx.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,preference,
                new CookieOptions{Path="/",Expires=DateTimeOffset.UtcNow.AddYears(1),IsEssential=true,SameSite=SameSiteMode.Strict,Secure=true,HttpOnly=true});
    }
    await next();
});

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline' https://maps.googleapis.com https://maps.gstatic.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com data:; img-src 'self' data: https:; connect-src 'self' https:; frame-ancestors 'none'; form-action 'self'; base-uri 'self'";
    if (ctx.Request.Path.StartsWithSegments("/conta") || ctx.Request.Path.StartsWithSegments("/editorial") || ctx.Request.Path.StartsWithSegments("/admin")) ctx.Response.Headers.CacheControl = "no-store";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseWhen(ctx => HttpMethods.IsGet(ctx.Request.Method), branch => branch.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}"));
app.UseRouting();

app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (ctx, next) =>
{
    if (ctx.User.Identity?.IsAuthenticated == true &&
        !ctx.Request.Path.StartsWithSegments("/conta") && !ctx.Request.Path.StartsWithSegments("/health"))
    {
        var user = await ctx.RequestServices.GetRequiredService<UserManager<ApplicationUser>>().GetUserAsync(ctx.User);
        if (user?.MustChangePassword == true) { ctx.Response.Redirect("/conta/perfil"); return; }
    }
    await next();
});
app.UseAuthorization();

app.MapGet("/idioma/mudar", (string cultura, string? returnUrl, HttpContext ctx) =>
{
    var normalized = SupportedCultures.Normalize(cultura);
    ctx.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(normalized)),
        new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Strict, Secure = true });

    if (!string.IsNullOrWhiteSpace(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
    {
        var trimmed = returnUrl.TrimStart('/');
        var parts = trimmed.Split('/', 2);
        if (parts.Length > 0 && SupportedCultures.All.Contains(parts[0], StringComparer.OrdinalIgnoreCase))
        {
            returnUrl = "/" + normalized + (parts.Length > 1 ? "/" + parts[1] : "");
        }
        return Results.LocalRedirect(returnUrl);
    }
    return Results.LocalRedirect("/" + normalized);
});

app.MapHealthChecks("/health");
app.MapControllerRoute("localized", "{culture:regex(^(pt-BR|en|fr|es|ru|zh-Hans|ja)$)}/{controller=Home}/{action=Index}/{id?}").RequireRateLimiting("public");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").RequireRateLimiting("public");

app.Run();
public partial class Program { }

