using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Infrastructure.MySql;
using MinhaBelem.Web.Models;
using MinhaBelem.Web.Resources;

namespace MinhaBelem.Tests;
[Collection("MySql")]
public sealed class AgendaCurationTests(PortalFixture fixture)
{
    private static CuratedAgenda Manifest()
    {
        var day = DateOnly.FromDateTime(PortalTime.Local(DateTime.UtcNow)).AddDays(1);
        var first = new DateOnly(day.Year, day.Month, 1).AddMonths(1);
        var source = "https://example.com/" + Guid.NewGuid().ToString("N");
        var item = new CuratedEvent("Sessão de teste", day, "11:00", null, "teatro", ContentCategory.Cultura,
            "Palco de teste", "Endereço de teste em Belém", "Campina", "Fonte de teste", source, "Resumo factual de teste", null, null, null);
        return new(day.AddDays(-1), day.AddDays(-1).AddMonths(3), DateTimeOffset.UtcNow,
            [item, item with { Time = "13:00" }, item with { Title = "Data sem horário", Time = null, SourceUrl = source + "/date" },
             item with { Title = "Encontro em vários dias", Date = first.AddDays(-1), LastDate = first.AddDays(1), Time = null, Subject = "congressos", SourceUrl = source + "/multi" }]);
    }
    [Fact] public void WholeManifestRejectsUnsafeSourcesAndInvalidTimesBeforeWrites()
    {
        var data = Manifest();
        Assert.Throws<ArgumentException>(() => CuratedAgendaImporter.Parse(JsonSerializer.Serialize(data with { Events = [data.Events[0] with { SourceUrl = "https://127.0.0.1/private" }] })));
        Assert.Throws<ArgumentException>(() => CuratedAgendaImporter.Parse(JsonSerializer.Serialize(data with { Events = [data.Events[0] with { Time = "25:00" }] })));
        Assert.Throws<ArgumentException>(() => CuratedAgendaImporter.Parse(JsonSerializer.Serialize(data with { Events = [data.Events[0] with { Category = ContentCategory.Politica }] })));
    }
    [Theory][InlineData("pt-BR")][InlineData("en")][InlineData("fr")][InlineData("es")][InlineData("ru")][InlineData("zh-Hans")][InlineData("ja")]
    public void AgendaControlsAreTranslated(string culture)
    {
        foreach (var key in new[] { "AgendaDiscover", "AgendaUnknownTime", "AgendaSave", "AgendaPeriod_90", "AgendaTopic_corrida", "AgendaIllustration" }) Assert.NotEqual(key, LocalizationCatalog.Get(key, culture));
    }
    [MySqlFact] public async Task ImportKeepsSessionsDistinctAndCalendarExportsProtectDraftsAndUnknownTimes()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContentRepository>();
        var importer = new CuratedAgendaImporter(repo); var manifest = Manifest();
        var first = await importer.ImportAsync(manifest, default);
        var again = await importer.ImportAsync(manifest, default);
        Assert.Equal(4, first.Inserted); Assert.Equal(4, again.Existing); Assert.Equal(0, again.Inserted); Assert.Equal(first.ContentIds, again.ContentIds);
        Assert.NotEqual(first.ContentIds[0], first.ContentIds[1]);
        using var client = fixture.Client();
        var known = await client.GetStringAsync($"/agenda/eventos/{first.ContentIds[0]}/calendario.ics");
        Assert.Contains("DTSTART:", known); Assert.DoesNotContain("DTEND:", known);
        foreach (var line in known.Split("\r\n")) Assert.True(Encoding.UTF8.GetByteCount(line) <= 75);
        var dateOnly = await client.GetStringAsync($"/agenda/eventos/{first.ContentIds[2]}/calendario.ics");
        Assert.Contains("DTSTART;VALUE=DATE:", dateOnly); Assert.DoesNotContain("T000000", dateOnly);
        var details = await client.GetStringAsync($"/Home/Detalhe/{first.ContentIds[2]}");
        Assert.Contains("Horário a confirmar", WebUtility.HtmlDecode(details)); Assert.DoesNotContain("Classificação Livre", WebUtility.HtmlDecode(details));
        var target = new DateOnly(manifest.Events[3].Date.Year, manifest.Events[3].Date.Month, 1).AddMonths(1);
        var overlap = await client.GetStringAsync($"/agenda?data={target:yyyy-MM-dd}");
        Assert.Contains("Encontro em vários dias", WebUtility.HtmlDecode(overlap));
        var draft = (await repo.GetAsync(first.ContentIds[0], default))!; draft.Status = ContentStatus.InReview;
        Assert.True(await repo.UpdateAsync(draft, null, "Revisão", default));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/agenda/eventos/{draft.Id}/calendario.ics")).StatusCode);
        foreach(var culture in SupportedCultures.All)
        {
            client.DefaultRequestHeaders.AcceptLanguage.Clear(); client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
            var page = await client.GetStringAsync("/agenda"); Assert.Contains("agenda-event-grid",page); Assert.DoesNotContain("AgendaDiscover",page);
        }
    }
}
