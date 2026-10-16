using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Services;
using MinhaBelem.Infrastructure.Identity;
using MinhaBelem.Infrastructure.Networking;

namespace MinhaBelem.Tests;

public class SecurityTests
{
    [Theory]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://10.0.0.1/")]
    [InlineData("https://[::1]/")]
    [InlineData("https://user:password@example.com/")]
    [InlineData("http://example.com/")]
    public void UnsafeUrlsAreRejected(string url) => Assert.False(SafeUrl.Validate(url).IsValid);

    [Fact]
    public void TotpMatchesRfcVectorAndRejectsExtraDigits()
    {
        var secret = Totp.Base32Encode(Encoding.ASCII.GetBytes("12345678901234567890"));
        var time = DateTime.UnixEpoch.AddSeconds(59);
        Assert.Equal("287082", Totp.CurrentCode(secret, time));
        Assert.Equal("287082", Totp.Verify(secret, "287082", time));
        Assert.Empty(Totp.Verify(secret, "2870829", time));
        Assert.Empty(Totp.Verify(secret, "287082abc", time));
        Assert.Empty(Totp.Verify(secret, "287082", time.AddMinutes(5)));
    }

    [Fact]
    public async Task DnsWithAnyPrivateAddressIsRejected()
    {
        var guard = new DnsGuard(new MixedResolver(), NullLogger<DnsGuard>.Instance);
        Assert.False(await guard.IsHostPublicAsync("example.com", default));
    }

    [Fact]
    public void InfrastructureDependenciesResolveWithoutDatabaseConnection()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Server=localhost;Database=minhabelem;User ID=test;Password=test;"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMinhaBelemInfrastructure(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAiAnalysisService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFeedReader>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRobotsGate>());
    }

    [Fact]
    public async Task UnrelatedRobotsAllowRuleCannotOverrideBlockedPath()
    {
        using var http = new HttpClient(new RobotsHandler());
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var gate = new MinhaBelem.Infrastructure.Feeds.RobotsGate(http, cache, NullLogger<MinhaBelem.Infrastructure.Feeds.RobotsGate>.Instance);
        Assert.False((await gate.IsAllowedAsync(new Uri("https://example.com/private/data"), default)).Allowed);
        Assert.True((await gate.IsAllowedAsync(new Uri("https://example.com/public/data"), default)).Allowed);
    }

    private sealed class RobotsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("User-agent: *\nDisallow: /private\nAllow: /unrelated-longer-path\n") });
    }

    private sealed class MixedResolver : IDnsResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct) =>
            Task.FromResult(new[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("192.168.1.2") });
    }
}
