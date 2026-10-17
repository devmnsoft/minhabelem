using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Text;
using MinhaBelem.Infrastructure.Discovery;

namespace MinhaBelem.Tests;
public sealed class DiscoveryTests
{
    [Fact]
    public void CalendarKeepsEventDateSeparateFromPostAndExpandsRecurringEvents()
    {
        const string calendar = "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//MinhaBelem Tests//PT\r\nBEGIN:VEVENT\r\nUID:festival\r\nDTSTAMP:20291201T120000Z\r\nDTSTART;TZID=America/Belem:20300105T180000\r\nDTEND;TZID=America/Belem:20300105T200000\r\nRRULE:FREQ=WEEKLY;COUNT=3\r\nSUMMARY:Festival em Belém\r\nDESCRIPTION:Evento confirmado\r\nLOCATION:Nazaré, Belém, Pará\r\nURL:https://example.com/festival\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";
        var result = CalendarConnector.Parse(calendar, "https://example.com/agenda.ics", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), 20);
        Assert.Equal(3, result.Count);
        Assert.Equal(new DateTime(2030, 1, 5, 21, 0, 0, DateTimeKind.Utc), result[0].Event!.StartUtc);
        Assert.Equal(2029, result[0].PostedAt!.Value.Year);
        Assert.Equal(3, result.Select(x => x.ExternalId).Distinct().Count());
    }
    [Fact]
    public void FloatingCalendarAndJsonLdWithoutTimezoneRemainUncertain()
    {
        const string html = """<script type="application/ld+json">{"@type":"Event","name":"Teatro em Belém","startDate":"2030-01-05T18:00:00","description":"Programação"}</script>""";
        var item = Assert.Single(StructuredEvents.ParseJsonLd(html, "https://example.com/teatro"));
        Assert.True(item.Event!.Uncertain);
        Assert.Null(item.Event.StartUtc);
        Assert.Null(item.PostedAt);
    }
    [Fact]
    public void JsonLdExtractsVerifiedFieldsWithoutInventingMissingValues()
    {
        const string html = """<script type='application/ld+json'>{"@graph":[{"@type":"Event","name":"Teatro em Belém","startDate":"2030-01-05T18:00:00-03:00","endDate":"2030-01-05T20:00:00-03:00","location":{"name":"Teatro","address":{"addressLocality":"Belém"}},"organizer":{"name":"Organizador"},"offers":{"price":0},"eventStatus":"https://schema.org/EventCancelled"}]}</script>""";
        var item = Assert.Single(StructuredEvents.ParseJsonLd(html, "https://example.com/teatro"));
        Assert.True(item.Event!.IsFree);
        Assert.Equal(21, item.Event.StartUtc!.Value.Hour);
        Assert.Contains("Cancelled", item.Event.Status);
        Assert.Null(item.Event.AgeRating);
    }
    [Fact]
    public void GeographyUsesParaCatalogAndRejectsPortugal()
    {
        Assert.Equal(144, BelemGeography.Municipalities.Length);
        Assert.Null(BelemGeography.ResolveMunicipality("Belém, Lisboa, Portugal"));
        Assert.Equal("Santa Bárbara do Pará", BelemGeography.ResolveMunicipality("Evento em Santa Bárbara do Pará")!.Name);
        Assert.Equal("Belém", BelemGeography.ResolveMunicipality("Programação de Mosqueiro")!.Name);
        Assert.Equal("Terra Firme", BelemGeography.ResolveNeighborhood("bairro Terra Firme, Belém"));
        Assert.DoesNotContain(BelemGeography.Municipalities, x => x.Name == "Pracuúba");
    }
    [Theory]
    [InlineData("https://[fd00::1]/")]
    [InlineData("https://[fc00::2]/")]
    [InlineData("https://[2001:db8::1]/")]
    public void ReservedIpv6IsBlocked(string url) => Assert.False(SafeUrl.Validate(url).IsValid);
    [Fact]
    public void PlainTextTruncationRespectsDatabaseColumnLimit()
    {
        Assert.Equal(1500, HtmlText.ToPlainText(new string('x', 2000), 1500).Length);
        Assert.Empty(HtmlText.Truncate("texto", 0));
    }
    [Theory]
    [InlineData(FeedProtocol.Instagram, "graph.instagram.com", "{\"data\":[{\"caption\":\"Evento\",\"permalink\":\"https://www.instagram.com/p/123\",\"timestamp\":\"2030-01-01T12:00:00Z\"}]}")]
    [InlineData(FeedProtocol.Facebook, "graph.facebook.com", "{\"data\":[{\"message\":\"Evento\",\"permalink_url\":\"https://www.facebook.com/123\"}]}")]
    [InlineData(FeedProtocol.TikTok, "open.tiktokapis.com", "{\"data\":{\"videos\":[{\"title\":\"Evento\",\"share_url\":\"https://www.tiktok.com/video/123\"}],\"cursor\":123}}")]
    [InlineData(FeedProtocol.YouTube, "www.googleapis.com", "{\"items\":[{\"id\":{\"videoId\":\"123\"},\"snippet\":{\"title\":\"Evento\",\"description\":\"Descrição\"}}]}")]
    [InlineData(FeedProtocol.WebDiscovery, "customsearch.googleapis.com", "{\"items\":[{\"title\":\"Evento\",\"link\":\"https://example.com/evento\",\"snippet\":\"Descrição\"}]}")]
    [InlineData(FeedProtocol.GovernmentApi, "example.com", "{\"items\":[{\"title\":\"Evento\",\"url\":\"https://example.com/evento\",\"summary\":\"Descrição\"}]}")]
    public async Task AuthorizedConnectorsParseOfficialResponseShapesAndKeepTokensOutOfUrls(FeedProtocol protocol, string host, string json)
    {
        var handler = new JsonHandler(json);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Sources:Credentials:fixture:Token"] = "test-token-only", ["Sources:Credentials:fixture:AllowedHost"] = host }).Build();
        var http = new ConnectorHttp(new Factory(handler), config);
        ISourceConnector connector = protocol switch
        {
            FeedProtocol.Instagram => new InstagramConnector(http), FeedProtocol.Facebook => new FacebookConnector(http),
            FeedProtocol.TikTok => new TikTokConnector(http), FeedProtocol.YouTube => new YouTubeConnector(http),
            FeedProtocol.WebDiscovery => new WebDiscoveryConnector(http), _ => new GovernmentDataConnector(http)
        };
        var context = new SourceContext(new IngestionSource { Protocol = protocol, FeedUrl = "https://" + host + "/endpoint" }, new SourceProfile { CredentialReference = "fixture" }, DateTime.UtcNow);
        Assert.Null(connector.ConfigurationIssue(context));
        var result = await connector.ReadAsync(context, default);
        Assert.Single(result.Items);
        Assert.DoesNotContain("test-token-only", handler.Url);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Null(result.Items[0].Event);
    }
    [Fact]
    public void SocialConnectorsRemainPendingWithoutCredential()
    {
        var http = new ConnectorHttp(new Factory(new JsonHandler("{}")), new ConfigurationBuilder().Build());
        var connector = new InstagramConnector(http);
        Assert.NotNull(connector.ConfigurationIssue(new(new IngestionSource { FeedUrl = "https://graph.instagram.com/me/media" }, new SourceProfile(), DateTime.UtcNow)));
    }
    [Fact]
    public async Task CredentialsCannotBeForwardedToAnotherHost()
    {
        var handler = new JsonHandler("{}");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Sources:Credentials:fixture:Token"] = "test-token-only", ["Sources:Credentials:fixture:AllowedHost"] = "graph.instagram.com" }).Build();
        var http = new ConnectorHttp(new Factory(handler), config);
        var context = new SourceContext(new IngestionSource { FeedUrl = "https://example.com/steal" }, new SourceProfile { CredentialReference = "fixture" }, DateTime.UtcNow);
        Assert.NotNull(http.CredentialIssue(context));
        await Assert.ThrowsAsync<InvalidOperationException>(() => http.ReadAsync(context, default));
        Assert.Empty(handler.Url);
    }
    internal sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory { public HttpClient CreateClient(string name) => new(handler, disposeHandler: false); }
    internal sealed class JsonHandler(string json) : HttpMessageHandler
    {
        public string Url { get; private set; } = "";
        public string? AuthorizationScheme { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Url = request.RequestUri!.AbsoluteUri; AuthorizationScheme = request.Headers.Authorization?.Scheme;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
