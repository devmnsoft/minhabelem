using System.Net;
using Dapper;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Web.Resources;

namespace MinhaBelem.Tests;

[Collection("MySql")]
public sealed class Sprint25ExperienceTests(PortalFixture fixture)
{
    [MySqlFact]
    public async Task PublishedEventAppearsInAgendaWithoutRepeatingInHomeNews()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IContentRepository>();
        var marker = Guid.NewGuid().ToString("N");
        var created = new List<long>();
        try
        {
            foreach (var (kind, status, title) in new[] {
                (ContentKind.Event, ContentStatus.Published, "Evento de teste descartável " + marker),
                (ContentKind.News, ContentStatus.Published, "Notícia de teste descartável " + marker),
                (ContentKind.Event, ContentStatus.InReview, "Em revisão " + marker) })
            {
                created.Add(await repository.InsertAsync(new ContentItem {
                    Kind = kind, Status = status, Title = title, Slug = Guid.NewGuid().ToString("N"),
                    Summary = "Fixture automatizada. Não representa programação real de Belém.",
                    Body = "Banco aleatório de teste; não publicado no produto.",
                    SourceName = "Teste descartável", SourceUrl = "https://example.com/" + Guid.NewGuid().ToString("N"),
                    VerificationStatus = VerificationStatus.Verified, PublishedAt = DateTime.UtcNow,
                    LastVerifiedAt = DateTime.UtcNow, RequiresHumanApproval = false,
                    EventStartAt = kind == ContentKind.Event ? DateTime.UtcNow.AddDays(2) : null,
                    EventEndAt = kind == ContentKind.Event ? DateTime.UtcNow.AddDays(2).AddHours(1) : null
                }, null, "Fixture Sprint 25", default));
            }
            using var client = fixture.Client();
            var html = WebUtility.HtmlDecode(await client.GetStringAsync("/"));
            var news = html[html.IndexOf("id=\"noticias\"", StringComparison.Ordinal)..html.IndexOf("home-events-highlight", StringComparison.Ordinal)];
            Assert.Contains("Notícia de teste descartável " + marker, news);
            Assert.DoesNotContain("Evento de teste descartável " + marker, news);
            Assert.Contains("Evento de teste descartável " + marker, html);
            Assert.DoesNotContain("Em revisão " + marker, html);
            var search = WebUtility.HtmlDecode(await client.GetStringAsync("/?busca=" + marker));
            Assert.Contains("Evento de teste descartável " + marker, search);
            Assert.Contains("Notícia de teste descartável " + marker, search);
            Assert.DoesNotContain("Em revisão " + marker, search);
            if (Environment.GetEnvironmentVariable("MINHABELEM_VISUAL_SNAPSHOTS") is { Length: > 0 } output)
            {
                var directory = Path.Combine(output, "pt-BR");
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "home-data.html"), await client.GetStringAsync("/"));
            }
        }
        finally
        {
            await using var db = await fixture.OpenAsync();
            if (created.Count > 0) await db.ExecuteAsync("DELETE FROM mb_content_items WHERE id IN @Ids", new { Ids = created });
        }
    }

    [MySqlFact]
    public async Task VisitorCanReachExistingJourneysAndSearchAcrossSevenLanguages()
    {
        using var client = fixture.Client();
        foreach (var culture in SupportedCultures.All)
        {
            client.DefaultRequestHeaders.AcceptLanguage.Clear();
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
            var html = await client.GetStringAsync("/");
            Assert.Contains(LocalizationCatalog.Get("CityQuestion", culture), WebUtility.HtmlDecode(html));
            foreach (var destination in new[] { "/agenda?dias=0", "/agenda", "/guia", "/como-chegar", "/mobilidade", "/mobilidade/de-para", "/mobilidade/paradas", "/mobilidade/fluvial", "/guia-ia", "/salvos", "/conta/login" })
            {
                Assert.Contains($"href=\"{destination}\"", html);
                var response = await client.GetAsync(destination);
                Assert.True(response.IsSuccessStatusCode, $"{culture} {destination}: {response.StatusCode}");
            }
            Assert.DoesNotContain("ai-float-btn", html);
            var search = await client.GetStringAsync("/?busca=Bel%C3%A9m");
            Assert.Contains("name=\"busca\"", search);
            Assert.Contains("Belém", WebUtility.HtmlDecode(search));
            if (Environment.GetEnvironmentVariable("MINHABELEM_VISUAL_SNAPSHOTS") is { Length: > 0 } output)
            {
                var directory = Path.Combine(output, culture);
                Directory.CreateDirectory(directory);
                html = Regex.Replace(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", "");
                await File.WriteAllTextAsync(Path.Combine(directory, "home.html"), html);
            }
        }
    }
}
