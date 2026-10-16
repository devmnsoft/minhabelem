using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using MinhaBelem.Core.Security;
using MinhaBelem.Infrastructure.Identity;
using MinhaBelem.Infrastructure.MySql;
using MinhaBelem.Web.Security;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("pt-BR");
builder.Services.AddControllersWithViews(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddMinhaBelemInfrastructure(builder.Configuration);
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
});
var app = builder.Build();
app.UseForwardedHeaders();
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
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: https:; connect-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'self'";
    if (ctx.Request.Path.StartsWithSegments("/conta") || ctx.Request.Path.StartsWithSegments("/editorial") || ctx.Request.Path.StartsWithSegments("/admin")) ctx.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseHttpsRedirection(); app.UseStaticFiles(); app.UseRouting();
app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.UseWhen(ctx => HttpMethods.IsGet(ctx.Request.Method), branch => branch.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}"));
app.MapHealthChecks("/health");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").RequireRateLimiting("public");
app.Run();
public partial class Program { }
