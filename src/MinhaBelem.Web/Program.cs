using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;
using MinhaBelem.Web.Services;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{o.Cookie.Name="MinhaBelem.Editor";o.Cookie.HttpOnly=true;o.Cookie.SecurePolicy=CookieSecurePolicy.Always;o.Cookie.SameSite=SameSiteMode.Strict;o.LoginPath="/editorial/login";o.ExpireTimeSpan=TimeSpan.FromHours(2);o.SlidingExpiration=false;});
builder.Services.AddAuthorization();
builder.Services.AddHttpClient("feed",c=>{c.Timeout=TimeSpan.FromSeconds(12);c.DefaultRequestHeaders.UserAgent.ParseAdd("MinhaBelemBot/1.0");});
builder.Services.AddSingleton<NpgsqlDataSource>(_=>NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Default")??throw new InvalidOperationException("Connection string required")));
builder.Services.AddScoped<ContentRepository>();
builder.Services.AddScoped<EditorialRepository>();
builder.Services.AddHttpClient("ai",c=>c.Timeout=TimeSpan.FromSeconds(25));
builder.Services.AddScoped<AiAssistant>();
builder.Services.AddHostedService<FeedCollector>();
builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("public",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
var app=builder.Build();
if(!app.Environment.IsDevelopment()){app.UseExceptionHandler("/Home/Error");app.UseHsts();}
app.UseHttpsRedirection();app.UseStaticFiles();app.UseRouting();app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();
app.Use(async(ctx,next)=>{ctx.Response.Headers["X-Content-Type-Options"]="nosniff";ctx.Response.Headers["Referrer-Policy"]="strict-origin-when-cross-origin";ctx.Response.Headers["X-Frame-Options"]="DENY";await next();});
app.MapHealthChecks("/health");
app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}").RequireRateLimiting("public");
app.Run();
public partial class Program {}