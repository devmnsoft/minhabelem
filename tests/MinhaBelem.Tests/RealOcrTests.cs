using System.Net;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
using MinhaBelem.Infrastructure.Discovery;
using MySqlConnector;
namespace MinhaBelem.Tests;
public sealed class OcrFactAttribute : FactAttribute
{
    public OcrFactAttribute(bool video = false)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MINHABELEM_TEST_MYSQL")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OCR_TEST_EXECUTABLE")))
            Skip = "Exige ambiente de homologação com MySQL descartável e Tesseract real.";
        else if (video && (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OCR_TEST_FFMPEG")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OCR_TEST_VIDEO"))))
            Skip = "Exige FFmpeg real e vídeo de teste configurados.";
    }
}
[Collection("MySql")]
public sealed class RealOcrTests(PortalFixture fixture)
{
    [OcrFact] public async Task AuthorizedPosterIsProcessedByRealTesseract() => await Run(false);
    [OcrFact(true)] public async Task AuthorizedVideoFrameIsProcessedByRealFfmpegAndTesseract() => await Run(true);
    private async Task Run(bool video)
    {
        var (client, actor) = await new OperationalTests(fixture).Login(); using var owner = client;
        using var scope = fixture.Factory.Services.CreateScope();
        var source = new IngestionSource { Name = "Cartaz próprio de teste", FeedUrl = "https://example.com/" + Guid.NewGuid().ToString("N"), Enabled = true, Approved = true, LicenseNote = "Material próprio criado para teste" };
        var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionRepository>(); source.Id = await ingestion.UpsertSourceAsync(source, default);
        var item = new DiscoveredItem("Cartaz", "Texto do cartaz de teste", source.FeedUrl + "/evento", "Fixture", DateTime.UtcNow) { MediaUrl = "https://example.com/poster", MediaCredit = "MinhaBelém teste", MediaLicense = "Material próprio de teste" };
        var stored = await scope.ServiceProvider.GetRequiredService<IDiscoveryRepository>().StoreAsync(source, item, await ingestion.StartRunAsync(default), default);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Ocr:ExecutablePath"] = Environment.GetEnvironmentVariable("OCR_TEST_EXECUTABLE"), ["Ocr:FfmpegPath"] = Environment.GetEnvironmentVariable("OCR_TEST_FFMPEG"), ["Ocr:AllowedMediaHosts:0"] = "example.com" }).Build();
        var bytes = await File.ReadAllBytesAsync(video ? Environment.GetEnvironmentVariable("OCR_TEST_VIDEO")! : Path.Combine(AppContext.BaseDirectory, "Fixtures", "poster.png"));
        var media = new TesseractMediaAnalysis(scope.ServiceProvider.GetRequiredService<MySqlDataSource>(), new MediaFactory(bytes, video ? "video/mp4" : "image/png"), config);
        Assert.True(media.IsConfigured);
        await Assert.ThrowsAsync<InvalidOperationException>(() => media.AnalyzeAsync(stored.Id, actor, default));
        await scope.ServiceProvider.GetRequiredService<IAdministrationRepository>().SetMediaPermissionAsync(source.Id, true, "Material de teste autorizado", actor, default);
        var result = await media.AnalyzeAsync(stored.Id, actor, default);
        Assert.Contains("NOVEMBRO", result.Text.ToUpperInvariant()); Assert.Contains("2030", result.Text);
        Assert.StartsWith("data:image/png;base64,", result.ThumbnailDataUrl);
        await using var db = await fixture.OpenAsync();
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_media_analysis WHERE candidate_id=@id AND actor_id=@actor", new { id = stored.Id, actor }));
    }
    private sealed class MediaFactory(byte[] image, string mime) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(image, mime));
        private sealed class Handler(byte[] image, string mime) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(image) };
                response.Content.Headers.ContentType = new(mime); return Task.FromResult(response);
            }
        }
    }
}
