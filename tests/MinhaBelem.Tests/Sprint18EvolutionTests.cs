using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Infrastructure.Background;
using MinhaBelem.Infrastructure.MySql;
using MinhaBelem.Web.Resources;
using MySqlConnector;

namespace MinhaBelem.Tests;

[Collection("MySql")]
public sealed class Sprint18EvolutionTests(PortalFixture fixture)
{
    [MySqlFact]
    public async Task RunningInstanceExposesOnlyConnectionMetadataToAuthorizedAdministrators()
    {
        using var anonymous=fixture.Client();
        Assert.Equal(System.Net.HttpStatusCode.Found,(await anonymous.GetAsync("/admin/ambiente")).StatusCode);
        var (moderator,_)=await new OperationalTests(fixture).Login(MinhaBelem.Core.Security.Roles.Moderator);
        using(moderator){var denied=await moderator.GetAsync("/admin/ambiente");Assert.Equal(System.Net.HttpStatusCode.Found,denied.StatusCode);Assert.Contains("/conta/acesso-negado",denied.Headers.Location!.ToString());}
        var (admin,_)=await new OperationalTests(fixture).Login();
        using(admin)
        {
            var response=await admin.GetAsync("/admin/ambiente");Assert.True(response.IsSuccessStatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(new[]{"database","databaseReference","environment","host","port"},json.RootElement.EnumerateObject().Select(x=>x.Name).OrderBy(x=>x));
            Assert.Equal("Development",json.RootElement.GetProperty("environment").GetString());
            Assert.Equal(MinhaBelem.Infrastructure.Identity.AuthenticationDiagnostics.DatabaseReference(fixture.ConnectionString),json.RootElement.GetProperty("databaseReference").GetString());
        }
    }

    [MySqlFact]
    public async Task BoatPlanningUsesOfficialSourceWithoutSendingVisitorOriginToGoogle()
    {
        using var scope=fixture.Factory.Services.CreateScope();
        var planner=scope.ServiceProvider.GetRequiredService<IRoutePlanner>();
        var result=await planner.PlanAsync(new RouteQuery{Origin="Origem privada fixture",Destination="Cotijuba",Mode="boat"},default);
        Assert.Equal("unavailable",result.State);Assert.Equal("SEGBEL",result.Provider);Assert.Empty(result.Options);
        Assert.Null(result.QueriedAt);Assert.Equal("https://seguranca.belem.pa.gov.br/",result.ExternalUrl);
        using var client=fixture.Client();
        var page=await client.GetStringAsync("/como-chegar?modo=boat");
        Assert.Contains("https://seguranca.belem.pa.gov.br/",page);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest,(await client.GetAsync("/como-chegar?modo=invalid")).StatusCode);
    }

    [MySqlFact]
    public async Task EvaluatingDiscoveredSourceRegistersItWithoutAuthorizingCollection()
    {
        using var scope=fixture.Factory.Services.CreateScope();
        var radar=scope.ServiceProvider.GetRequiredService<IRadarRepository>();
        var sources=scope.ServiceProvider.GetRequiredService<IIngestionRepository>();
        var url="https://example.com/"+Guid.NewGuid().ToString("N");
        var id=await radar.InsertDiscoveredSourceAsync(new DiscoveredSource{SourceName="Fonte editorial fixture",Url=url},default);
        Assert.True(await radar.EvaluateDiscoveredSourceAsync(id,SourceEvaluationStatus.Approved,"Avaliação editorial",null,default));
        var source=Assert.Single(await sources.SourcesAsync(false,default),x=>x.FeedUrl==url);
        Assert.False(source.Enabled);Assert.False(source.Approved);
        Assert.DoesNotContain(await sources.SourcesAsync(true,default),x=>x.Id==source.Id);
        Assert.False(await radar.ToggleSourceStateAsync(source.Id,true,default));
        Assert.False((await sources.SourceAsync(source.Id,default))!.Enabled);
    }

    [MySqlFact]
    public async Task ReevaluationDoesNotOverwriteAnExistingSourceAuthorization()
    {
        using var scope=fixture.Factory.Services.CreateScope();
        var radar=scope.ServiceProvider.GetRequiredService<IRadarRepository>();
        var sources=scope.ServiceProvider.GetRequiredService<IIngestionRepository>();
        var url="https://example.com/"+Guid.NewGuid().ToString("N");
        var source=new IngestionSource{Name="Fonte fixture autorizada",FeedUrl=url,Enabled=true,Approved=true,LicenseNote="Autorização fixture preservada"};
        source.Id=await sources.UpsertSourceAsync(source,default);
        var id=await radar.InsertDiscoveredSourceAsync(new DiscoveredSource{SourceName="Reavaliação fixture",Url=url},default);
        await radar.EvaluateDiscoveredSourceAsync(id,SourceEvaluationStatus.Approved,"Revisão",null,default);
        var existing=(await sources.SourceAsync(source.Id,default))!;
        Assert.True(existing.Enabled);Assert.True(existing.Approved);Assert.Equal(source.LicenseNote,existing.LicenseNote);
    }

    [MySqlFact]
    public async Task OperationsAuditReportsConfigurationPresenceWithoutKeyValues()
    {
        using var scope=fixture.Factory.Services.CreateScope();
        var secret="private-fixture-"+Guid.NewGuid().ToString("N");
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["GoogleRoutes:ApiKey"]=secret,["Maps:BrowserKey"]=secret,["GoogleRoutes:Enabled"]="true",["GoogleRoutes:DailyLimit"]="5"
        }).Build();
        var json=JsonSerializer.Serialize(await OperationsDiagnostics.ReadLocalAsync(scope.ServiceProvider.GetRequiredService<MySqlDataSource>(),config));
        Assert.False(json.Contains(secret,StringComparison.Ordinal));
        using var snapshot=JsonDocument.Parse(json);var routes=snapshot.RootElement.GetProperty("routes");
        Assert.True(routes.GetProperty("serverKeyConfigured").GetBoolean());Assert.True(routes.GetProperty("browserKeyConfigured").GetBoolean());
        Assert.False(routes.GetProperty("providerRequestPerformed").GetBoolean());Assert.False(snapshot.RootElement.GetProperty("continuousCollectionProven").GetBoolean());
    }

    [MySqlFact]
    public async Task DashboardSeparatesFailingSourcesFromCompletedRunsAndDoesNotCountRunningAsFailure()
    {
        using var scope=fixture.Factory.Services.CreateScope();
        var dashboard=scope.ServiceProvider.GetRequiredService<IDashboardRepository>();var ingestion=scope.ServiceProvider.GetRequiredService<IIngestionRepository>();
        var before=await dashboard.BuildAsync(default);
        var source=new IngestionSource{Name="Falha fixture",FeedUrl="https://example.com/"+Guid.NewGuid(),Enabled=true,Approved=true,LicenseNote="Fixture"};
        source.Id=await ingestion.UpsertSourceAsync(source,default);
        await using var db=await fixture.OpenAsync();await db.ExecuteAsync("UPDATE mb_ingestion_sources SET consecutive_failures=3 WHERE id=@Id",source);
        await ingestion.StartRunAsync(default);
        foreach(var status in new[]{RunStatus.Partial,RunStatus.Failed})
        {
            var id=await ingestion.StartRunAsync(default);
            await ingestion.FinishRunAsync(new IngestionRun{Id=id,FinishedAt=DateTime.UtcNow,Status=status},default);
        }
        var after=await dashboard.BuildAsync(default);
        Assert.Equal(before.FailingSources+1,after.FailingSources);
        Assert.Equal(before.IngestionRunsWithErrorsLast24h+2,after.IngestionRunsWithErrorsLast24h);
    }

    [MySqlFact]
    public async Task ReviewedImportCanStageFreeEventsWithoutPublishingOrOverwritingEditorialWork()
    {
        using var scope=fixture.Factory.Services.CreateScope();var content=scope.ServiceProvider.GetRequiredService<IContentRepository>();
        var importer=new CuratedAgendaImporter(content);var today=DateOnly.FromDateTime(DateTime.UtcNow);
        var data=new CuratedAgenda(today,today.AddDays(7),DateTimeOffset.UtcNow,[new CuratedEvent(
            "Evento gratuito fixture",today.AddDays(2),"17:30",null,"teatro",ContentCategory.Cultura,"Local fixture","Endereço fixture",null,
            "Fonte fixture","https://example.com/"+Guid.NewGuid().ToString("N"),"Resumo factual fixture.","Gratuito",null,null,IsFree:true)]);
        var first=await importer.ImportAsync(data,default,stageForReview:true);
        var staged=(await content.GetAsync(Assert.Single(first.ContentIds),default))!;
        Assert.Equal(ContentStatus.InReview,staged.Status);Assert.True(staged.RequiresHumanApproval);Assert.True(staged.IsFree);
        var duplicate=await importer.ImportAsync(data,default);
        Assert.Equal(0,duplicate.Inserted);Assert.Equal(1,duplicate.Existing);
        Assert.Equal(ContentStatus.InReview,(await content.GetAsync(staged.Id,default))!.Status);
    }

    [Fact]
    public void NewPublicAndOperationalComponentsHaveSevenLanguageCopy()
    {
        foreach(var culture in SupportedCultures.All)
            foreach(var key in new[]{"CityWays","CityWaysIntro","PhotoArchiveNote","GuideIntro","GuideMapLoading","GuideUnavailable","RadarNoAuthorizedSources","RadarEmptyCycle","InterfaceOffline","SkipToContent","AccountPortal"})
                Assert.NotEqual(key,LocalizationCatalog.Get(key,culture));
    }

    [MySqlFact]
    public async Task LanguageChosenByUrlSurvivesNavigationToAnUnprefixedPage()
    {
        foreach(var culture in SupportedCultures.All)
        {
            using var client=fixture.Client();
            Assert.True((await client.GetAsync("/"+culture)).IsSuccessStatusCode);
            var next=await client.GetStringAsync("/guia");
            Assert.Contains("<html lang=\""+culture+"\">",next);
            Assert.Contains(LocalizationCatalog.Get("GuideIntro",culture),System.Net.WebUtility.HtmlDecode(next));
        }
    }

    [MySqlFact]
    public async Task ModeratorDoesNotSeePublicationControlsAndCannotForgePublishAction()
    {
        var (client,_)=await new OperationalTests(fixture).Login(MinhaBelem.Core.Security.Roles.Moderator);
        using(client)
        {
            var page=await client.GetStringAsync("/editorial/lotes?kind=content");
            Assert.DoesNotContain("<option value=\"publish\">",page);Assert.DoesNotContain("<option value=\"category\">",page);
            Assert.Contains("<option value=\"analyze\">",page);
            var forged=await PortalFixture.Post(client,"/editorial/lotes/previa","/editorial/lotes",new(){["Action"]="publish",["Reason"]="Tentativa sem permissão",["kind"]="content"});
            Assert.Equal(System.Net.HttpStatusCode.Found,forged.StatusCode);
            Assert.StartsWith("https://localhost/conta/acesso-negado",forged.Headers.Location!.ToString());
            Assert.Equal(System.Net.HttpStatusCode.Forbidden,(await client.GetAsync(forged.Headers.Location)).StatusCode);
        }
    }
}
