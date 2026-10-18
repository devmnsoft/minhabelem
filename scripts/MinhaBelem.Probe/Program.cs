using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Entities;
using MinhaBelem.Infrastructure.MySql;
using MinhaBelem.Infrastructure.Discovery;
using MySqlConnector;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
// Read-only verification: no database, content publication, images or full article copies.
var services=new ServiceCollection();var configuration=new ConfigurationBuilder().Build();
services.AddSingleton<IConfiguration>(configuration);services.AddLogging();services.AddMinhaBelemInfrastructure(configuration);
await using var provider=services.BuildServiceProvider();await using var scope=provider.CreateAsyncScope();
var results=new List<object>();
var observed=new List<DiscoveredItem>();
foreach(var url in new[]{"https://www.theatrodapaz.com.br/informa-es-do-evento-e-registro/sonho-encantado-de-cordel-o-musical-2026-10-17-20-00","https://www.theatrodapaz.com.br/informa-es-do-evento-e-registro/fida-festival-internacional-de-danca-da-amazonia-2026-10-21-09-00"})
{
    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(35));
    try
    {
        var robots=await scope.ServiceProvider.GetRequiredService<IRobotsGate>().IsAllowedAsync(new Uri(url),timeout.Token);
        if(!robots.Allowed){results.Add(new{url,state="robots_blocked",queriedAtUtc=DateTime.UtcNow});continue;}
        using var response=await provider.GetRequiredService<IHttpClientFactory>().CreateClient("feed").GetAsync(url,timeout.Token);
        if(!response.IsSuccessStatusCode){results.Add(new{url,state="http_"+(int)response.StatusCode,queriedAtUtc=DateTime.UtcNow});continue;}
        var html=await response.Content.ReadAsStringAsync(timeout.Token);if(html.Length>2_000_000)throw new InvalidOperationException();
        var items=scope.ServiceProvider.GetRequiredService<IEventStructuredDataExtractor>().ExtractEvents(html,url);
        observed.AddRange(items.Select(i=>new DiscoveredItem(i.Title,"Programação factual consultada na fonte oficial; confirme as informações antes de visitar.",i.Url,null,i.PostedAt,i.Event)));
        results.Add(new{url,state="consulted",queriedAtUtc=DateTime.UtcNow,futureEvents=items.Count(i=>i.Event?.StartUtc>DateTime.UtcNow),events=items.Select(i=>new{i.Title,i.Url,i.Event?.StartUtc,i.Event?.EndUtc,i.Event?.Location}),continuousReuseAuthorized=false});
    }
    catch(Exception ex){results.Add(new{url,state="unavailable",errorType=ex.GetType().Name,queriedAtUtc=DateTime.UtcNow});}
}
Directory.CreateDirectory("TestResults/sources");await File.WriteAllTextAsync("TestResults/sources/official-probe.json",JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("Consulta pontual concluída. Resultados factuais e limitações: TestResults/sources/official-probe.json");
if (args.Contains("--stage-local-review"))
{
    var localConfig = new ConfigurationBuilder().AddUserSecrets("MinhaBelem.Web").AddEnvironmentVariables().Build();
    var localConnection = new MySqlConnectionStringBuilder(localConfig.GetConnectionString("Default") ?? "");
    if (localConnection.Server is not ("localhost" or "127.0.0.1") || string.IsNullOrWhiteSpace(localConnection.Database))
        throw new InvalidOperationException("Encaminhamento exclusivo ao banco local configurado em User Secrets.");
    localConnection.AllowUserVariables = true;
    localConnection.DateTimeKind = MySqlDateTimeKind.Utc;
    await using var localData = new MySqlDataSourceBuilder(localConnection.ConnectionString).Build();
    var ingestion = new IngestionRepository(localData);
    var discovery = new DiscoveryRepository(localData);
    const string catalogUrl = "https://www.theatrodapaz.com.br/programa%C3%A7%C3%A3o";
    var source = (await ingestion.SourcesAsync(false, default)).FirstOrDefault(s => s.FeedUrl.TrimEnd('/') == catalogUrl);
    if (source is null)
    {
        source = new IngestionSource { Name = "Theatro da Paz — consulta factual pontual", FeedUrl = catalogUrl, Protocol = FeedProtocol.JsonLd,
            Enabled = false, Approved = false, RespectRobots = true,
            LicenseNote = "Consulta factual pontual solicitada na Sprint 17. Sem autorização contínua ou reutilização de mídia; exige revisão editorial e dos termos." };
        source.Id = await ingestion.UpsertSourceAsync(source, default);
        await discovery.SaveProfileAsync(new() { SourceId = source.Id, FrequencyMinutes = 1440, DailyRequestLimit = 1, MaxItems = 10 }, default);
    }
    var inserted = 0; var unchanged = 0;
    foreach (var item in observed.Where(x => x.Event?.StartUtc > DateTime.UtcNow))
    {
        if ((await discovery.StoreAsync(source, item, 0, default)).Changed) inserted++; else unchanged++;
    }
    await File.WriteAllTextAsync("TestResults/sources/local-review.json", JsonSerializer.Serialize(new
    {
        mode = "local_editorial_review", inserted, unchanged, publishedByThisCommand = 0,
        sourceActivatedByThisCommand = false, verifiedAtUtc = DateTime.UtcNow
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Candidatos futuros encaminhados à revisão local: {inserted}; sem mudanças: {unchanged}. Nenhuma publicação ou ativação automática.");
}
if(args.Contains("--persist-disposable")&&observed.Count>0)
{
    var server=Environment.GetEnvironmentVariable("MINHABELEM_TEST_MYSQL")??throw new InvalidOperationException("Configure somente o servidor MySQL descartável.");
    var cs=new MySqlConnectionStringBuilder(server);
    if(cs.Server is not("localhost" or "127.0.0.1")||!string.IsNullOrWhiteSpace(cs.Database))throw new InvalidOperationException("Servidor descartável local sem Database obrigatório.");
    var name="minhabelem_probe_"+Guid.NewGuid().ToString("N");
    await using var root=new MySqlConnection(server);await root.OpenAsync();await root.ExecuteAsync($"CREATE DATABASE `{name}` CHARACTER SET utf8mb4");
    try
    {
        cs.Database=name;cs.AllowUserVariables=true;cs.DateTimeKind=MySqlDateTimeKind.Utc;
        await using var data=new MySqlDataSourceBuilder(cs.ConnectionString).Build();
        await new SchemaMigrator(data,NullLogger<SchemaMigrator>.Instance).ApplyAsync();
        var ingestion=new IngestionRepository(data);var discovery=new DiscoveryRepository(data);var workflow=new EditorialWorkflow(data);var content=new ContentRepository(data);
        var source=new IngestionSource{Name="Theatro da Paz — consulta pontual autorizada de homologação",FeedUrl="https://www.theatrodapaz.com.br/programa%C3%A7%C3%A3o",Protocol=FeedProtocol.JsonLd,Enabled=false,Approved=false,LicenseNote="Consulta factual pontual, sem cópia de mídia ou autorização para coleta contínua."};
        source.Id=await ingestion.UpsertSourceAsync(source,default);var inserted=0;var duplicates=0;
        foreach(var item in observed)
        {
            var stored=await discovery.StoreAsync(source,item,0,default);if(stored.Changed)inserted++;
            if(!(await discovery.StoreAsync(source,item,0,default)).Changed)duplicates++;
            var id=await workflow.ImportAsync(stored.Id,null,default);
            if(id is {} contentId){var draft=(await content.GetAsync(contentId,default))!;draft.EventDetails=item.Event;draft.Kind=ContentKind.Event;draft.EventStartAt=item.Event?.StartUtc;draft.EventEndAt=item.Event?.EndUtc;draft.Address=item.Event?.Location;draft.Status=ContentStatus.InReview;draft.RequiresHumanApproval=true;await content.UpdateAsync(draft,null,"Evidência real encaminhada para revisão no banco descartável",default);}
        }
        await using var db=await data.OpenConnectionAsync();var published=await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_content_items WHERE status='Published'");
        await File.WriteAllTextAsync("TestResults/sources/disposable-ingestion.json",JsonSerializer.Serialize(new{mode="discarded_database",inserted,duplicates,futureEvents=observed.Count(i=>i.Event?.StartUtc>DateTime.UtcNow),published,continuousSources=0,verifiedAtUtc=DateTime.UtcNow},new JsonSerializerOptions{WriteIndented=true}));
        if(published!=0||inserted!=observed.Count||duplicates!=observed.Count)throw new InvalidOperationException("Falha na rastreabilidade da consulta real.");
        Console.WriteLine("Metadados oficiais persistidos, deduplicados e encaminhados para revisão no banco descartável. Publicações: 0.");
    }
    finally
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^minhabelem_probe_[a-f0-9]{32}$"))throw new InvalidOperationException();
        await root.ExecuteAsync($"DROP DATABASE `{name}`");
    }
}
