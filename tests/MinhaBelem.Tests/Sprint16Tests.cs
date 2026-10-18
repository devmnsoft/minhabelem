using System.Net;
using System.Security.Claims;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Policies;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Services;
using MinhaBelem.Infrastructure.Discovery;
using MinhaBelem.Infrastructure.Identity;
using MinhaBelem.Web.Models;
using MinhaBelem.Web.Resources;

namespace MinhaBelem.Tests;
[Collection("MySql")]
public sealed class Sprint16Tests(PortalFixture fixture)
{
    [MySqlFact] public async Task TransitLineSavesEvidenceAndUpdatesItsFareValidity()
    {
        using var scope=fixture.Factory.Services.CreateScope();var transit=scope.ServiceProvider.GetRequiredService<ITransitRepository>();
        var line=new TransitLine{Code="S16-"+Guid.NewGuid().ToString("N")[..8],Name="Linha descartável S16",Origin="Origem fixture",Destination="Destino fixture",Fare=2,SourceUrl="https://example.com/operator",VerifiedAt=DateTime.UtcNow.AddHours(-1),ValidUntil=DateTime.UtcNow.AddDays(2),FareValidUntil=DateTime.UtcNow.AddDays(1)};
        var id=await transit.UpsertLineAsync(line,default);Assert.True(id>0);Assert.Equal(2,(await transit.GetLineByCodeAsync(line.Code,default))!.ConfirmedFare);
        line.Fare=3;line.FareValidUntil=DateTime.UtcNow.AddMinutes(-1);Assert.Equal(id,await transit.UpsertLineAsync(line,default));
        var updated=(await transit.GetLineByCodeAsync(line.Code,default))!;Assert.Equal(3,updated.Fare);Assert.Null(updated.ConfirmedFare);Assert.Equal(line.SourceUrl,updated.SourceUrl);
    }
    [MySqlFact] public async Task GroupBudgetAndAvailableTimeRequireAFeasibleReturn()
    {
        var(client,actor)=await new OperationalTests(fixture).Login();using(client)using(var scope=fixture.Factory.Services.CreateScope())
        {
            var places=scope.ServiceProvider.GetRequiredService<IPlaceRepository>();var visits=scope.ServiceProvider.GetRequiredService<IVisitRepository>();
            var id=await places.UpsertAsync(new(){GooglePlaceId="s16-"+Guid.NewGuid(),Name="Visita S16",PrimaryType="museum",IsPublished=true,Latitude=-1.45,Longitude=-48.49,FormattedAddress="Endereço de teste",PurgeAfter=DateTime.UtcNow.AddDays(7)},default);
            try
            {
            Assert.True(await visits.SaveAsync(new(){PlaceId=id,SourceUrl="https://example.com/open",ConfirmedAt=DateTime.UtcNow.AddHours(-1),ValidUntil=DateTime.UtcNow.AddDays(7),EntryPrice=10},Enumerable.Range(0,7).Select(d=>new VisitHours{Weekday=d,Is24h=true}).ToArray(),0,actor,default));
            var request=new ItineraryRequest{Origin="Origem S16",LocalStart=PortalTime.Local(DateTime.UtcNow).Date.AddDays(1).AddHours(9),Category="museum",AvailableMinutes=480};
            var planner=new MinhaBelem.Infrastructure.Places.ItineraryPlanning(places,visits,new TestRoutes(false));
            var one=await planner.PlanAsync(request,"pt-BR",default);Assert.Equal("calculated",one.State);
            request.People=3;var three=await planner.PlanAsync(request,"pt-BR",default);Assert.Equal(one.Days.Sum(d=>d.KnownCost)*3,three.Days.Sum(d=>d.KnownCost));Assert.All(three.Days.Where(d=>d.Visits.Count>0),d=>Assert.True(d.ReturnTimeConfirmed));
            var impossible=new MinhaBelem.Infrastructure.Places.ItineraryPlanning(places,visits,new TestRoutes(true));Assert.Equal("unavailable",(await impossible.PlanAsync(request,"pt-BR",default)).State);
            request.AvailableMinutes=60;Assert.Equal("unavailable",(await planner.PlanAsync(request,"pt-BR",default)).State);
            }
            finally { await places.SetPublishedAsync(id,false,actor,default); }
        }
    }
    private sealed class TestRoutes(bool lateReturn):IRoutePlanner
    {
        public Task<RoutePlan> PlanAsync(RouteQuery q,CancellationToken ct)=>Task.FromResult(new RoutePlan("available","fixture","Fixture",DateTime.UtcNow,[new("Fixture",1000,lateReturn&&q.Destination=="Origem S16"?86400:600,[new("TRANSIT","Trecho fixture",1000,"TEST","A","B",null,null)],2,"BRL",null)],"https://example.com/route"));
    }
    [Fact] public void StructuredSessionsKeepIdentityAcrossRescheduleAndHeuristicsNeverInventEveningHours()
    {
        const string html="""<script type="application/ld+json">{"@type":"EventSeries","name":"Festival","subEvent":[{"@type":"Event","@id":"slot-1","name":"Sessão","startDate":"2027-01-02T10:00:00-03:00","location":"Belém"},{"@type":"Event","@id":"slot-2","name":"Sessão","startDate":"2027-01-02T15:00:00-03:00","location":"Belém"}]}</script>""";
        var rows=StructuredEvents.ParseJsonLd(html,"https://example.com/festival").Where(x=>x.Event?.StartUtc is not null).ToArray();
        Assert.Equal(2,rows.Length);Assert.Equal(2,rows.Select(x=>x.ExternalId).Distinct().Count());
        var moved=StructuredEvents.ParseJsonLd(html.Replace("10:00:00","11:00:00"),"https://example.com/festival").First(x=>x.ExternalId=="slot-1");Assert.Equal(rows[0].ExternalId,moved.ExternalId);Assert.NotEqual(rows[0].Event!.StartUtc,moved.Event!.StartUtc);
        var text=new EventStructuredDataExtractor().ExtractEventFromText("Data: 20/11/2027 Local: Belém", "https://example.com/event");
        Assert.NotNull(text);Assert.True(text.Uncertain);Assert.NotEqual(22,text.StartUtc!.Value.Hour);
        var date=StructuredEvents.ParseJsonLd("""<script type="application/ld+json">{"@type":"Event","name":"Data","startDate":"2027-01-02","location":"Belém"}</script>""","https://example.com/date").Single();
        Assert.True(date.Event!.DateOnly);Assert.True(date.Event.Uncertain);
    }
    [Fact] public void SuggestedVisitFitsConfirmedOpeningWindowAndRejectsStaleEvidence()
    {
        var local=PortalTime.Local(DateTime.UtcNow).Date.AddDays(1);var now=DateTime.UtcNow;
        var schedule=new VisitSchedule(new(){SourceUrl="https://example.com/hours",ConfirmedAt=now.AddHours(-1),ValidUntil=now.AddDays(5)},[new(){Weekday=(int)local.DayOfWeek,OpenMinute=600,CloseMinute=660}]);
        var after=PortalTime.Utc(local.AddHours(8));Assert.Null(VisitRecommendationService.Suggest(schedule,after,now,90));
        var period=VisitRecommendationService.Suggest(schedule,after,now,30);Assert.NotNull(period);Assert.Equal(10,PortalTime.Local(period.Value.StartUtc).Hour);
        schedule.Info!.ValidUntil=now.AddMinutes(-1);Assert.Null(VisitRecommendationService.Suggest(schedule,after,now,30));
    }
    [Theory][InlineData("pt-BR")][InlineData("en")][InlineData("fr")][InlineData("es")][InlineData("ru")][InlineData("zh-Hans")][InlineData("ja")]
    public void NewControlsHaveSevenLanguages(string culture)
    {foreach(var key in new[]{"PhotoCredits","Agenda365Title","RiverEditor","BatchProgress","BatchHistory","VisitSuggestionReason","ItineraryPeople"})Assert.NotEqual(key,LocalizationCatalog.Get(key,culture));}
    [MySqlFact] public async Task DiscoveryHistoryPreservesAtoBtoAAndDuplicateReadsAreIdempotent()
    {
        using var scope=fixture.Factory.Services.CreateScope();var ingestion=scope.ServiceProvider.GetRequiredService<IIngestionRepository>();var discovery=scope.ServiceProvider.GetRequiredService<IDiscoveryRepository>();
        var source=new IngestionSource{Name="Fonte de teste S16",FeedUrl="https://example.com/"+Guid.NewGuid(),Enabled=false};source.Id=await ingestion.UpsertSourceAsync(source,default);
        var original=new DiscoveredItem("Evento de teste", "Resumo A",source.FeedUrl,null,null,new(DateTime.UtcNow.AddDays(20),null,"Belém","Organizador","Scheduled",null,false,null,null,source.FeedUrl),"stable-event");
        var a=await discovery.StoreAsync(source,original,0,default);var b=await discovery.StoreAsync(source,original with{Summary="Resumo B",Event=original.Event! with{Status="EventCancelled"}},0,default);var again=await discovery.StoreAsync(source,original,0,default);
        Assert.Equal(a.Id,b.Id);Assert.Equal(a.Id,again.Id);Assert.True(again.Changed);Assert.False((await discovery.StoreAsync(source,original,0,default)).Changed);Assert.Equal("Scheduled",(await discovery.EvidenceAsync(a.Id,default))!.Status);
        await using var db=await fixture.OpenAsync();Assert.Equal(3,await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_discovery_versions WHERE candidate_id=@Id",new{a.Id}));
    }
    [MySqlFact] public async Task TwoHundredFilteredItemsPreviewConfirmExecuteAndReportOwnerOnlyProgress()
    {
        var(client,actor)=await new OperationalTests(fixture).Login();using(client)using(var scope=fixture.Factory.Services.CreateScope())
        {
            var prefix="s16"+Guid.NewGuid().ToString("N");await using var db=await fixture.OpenAsync();
            var values=string.Join(",",Enumerable.Range(0,200).Select(i=>$"('News','Cultura',@prefix,CONCAT(@prefix,'-{i}'),@prefix,'Fixture','https://example.com',UNHEX(SHA2(CONCAT(@prefix,'{i}'),256)),SHA2(CONCAT(@prefix,'{i}'),256))"));
            await db.ExecuteAsync("INSERT INTO mb_content_items(kind,category,title,slug,summary,source_name,source_url,source_url_hash,fingerprint) VALUES "+values,new{prefix});
            var preview=await PortalFixture.Post(client,"/editorial/lotes/previa","/editorial/lotes?search="+prefix,new(){{"Action","review"},{"Reason","Revisão de duzentos registros selecionados"},{"kind","content"},{"search",prefix},{"allFiltered","true"},{"confirmedCount","200"}});
            Assert.Equal(HttpStatusCode.Redirect,preview.StatusCode);var location=preview.Headers.Location!.ToString();var id=location.Split('/').Last();
            await PortalFixture.Post(client,location+"/confirmar",location,new(){{"confirmed","true"}});var batches=scope.ServiceProvider.GetRequiredService<IEditorialBatches>();
            for(var i=0;i<10&&(await batches.GetAsync(id,actor,default))!.Status!="completed";i++)await batches.ProcessNextAsync(default);
            var result=(await batches.GetAsync(id,actor,default))!;Assert.Equal(200,result.Items.Count);Assert.All(result.Items,x=>Assert.Equal("completed",x.Status));
            Assert.Contains((await batches.HistoryAsync(actor,default)),x=>x.Id==id);Assert.Empty(await batches.HistoryAsync(actor+99999,default));
            var progress=await client.GetAsync(location+"/progresso");Assert.Equal(HttpStatusCode.OK,progress.StatusCode);Assert.Contains("\"finished\":200",await progress.Content.ReadAsStringAsync());
            if(Environment.GetEnvironmentVariable("MINHABELEM_VISUAL_SNAPSHOTS") is {Length:>0} output)
            foreach(var culture in SupportedCultures.All)
            {
                client.DefaultRequestHeaders.AcceptLanguage.Clear();client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
                var response=await client.GetAsync(location);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
                var html=System.Text.RegularExpressions.Regex.Replace(await response.Content.ReadAsStringAsync(),"<input[^>]*name=\"__RequestVerificationToken\"[^>]*>","");
                var directory=Path.Combine(output,culture);Directory.CreateDirectory(directory);await File.WriteAllTextAsync(Path.Combine(directory,"editorial-batch-detail.html"),html);
            }
            var content=scope.ServiceProvider.GetRequiredService<IContentRepository>();var item=(await content.GetAsync(result.Items[0].TargetId,default))!;
            var principal=new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,actor.ToString()),new("amr","mfa"),new(PermissionClaimsPrincipalFactory.PermissionClaimType,Permissions.ContentWrite)],"test"));
            var priority=await batches.PreviewAsync(new(){Action="priority",Value="3",Reason="Prioridade editorial confirmada",Targets=[new(item.Id,"content",item.Revision)]},principal,default);await batches.ConfirmAsync(priority,principal,default);await batches.ProcessNextAsync(default);Assert.Equal(3,(await content.GetAsync(item.Id,default))!.EditorialPriority);
            using var anonymous=fixture.Client();Assert.Equal(HttpStatusCode.Redirect,(await anonymous.GetAsync(location+"/progresso")).StatusCode);
        }
    }
    [MySqlFact] public async Task RiverAdministrationRequiresVerificationAndPreservesConcurrentEdits()
    {
        var(client,actor)=await new OperationalTests(fixture).Login();using(client)
        {
            var response=await PortalFixture.Post(client,"/admin/mobilidade/fluvial","/admin/mobilidade/fluvial",new(){{"Name","Travessia fixture"},{"DeparturePort","Terminal de teste"},{"DestinationPort","Destino de teste"},{"DestinationIsland","Cotijuba"},{"IsActive","true"}});Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
            using var scope=fixture.Factory.Services.CreateScope();var transit=scope.ServiceProvider.GetRequiredService<ITransitRepository>();
            var route=new RiverRoute{Name="Travessia descartável S16",DeparturePort="Origem fixture",DestinationPort="Destino fixture",IsActive=false};
            Assert.True(await transit.SaveRiverRouteAsync(route,1,actor,default));
            Assert.True(await transit.SaveRiverRouteAsync(route,1,actor,default));Assert.False(await transit.SaveRiverRouteAsync(route,1,actor,default));
            Assert.DoesNotContain(await transit.GetRiverRoutesAsync(default),x=>x.Id==route.Id);
            Assert.Contains(await transit.GetRiverRoutesAsync(default,true),x=>x.Id==route.Id);
        }
    }
}
