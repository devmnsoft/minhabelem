using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Policies;
using MinhaBelem.Infrastructure.Places;
using MinhaBelem.Web.Resources;
namespace MinhaBelem.Tests;
[Collection("MySql")]
public sealed class Sprint15bTests(PortalFixture fixture)
{
    private static readonly DateTime Now=new(2026,10,18,15,0,0,DateTimeKind.Utc);
    private static VisitSchedule Schedule(params VisitHours[] hours)=>new(new(){ConfirmedAt=Now.AddDays(-1),ValidUntil=Now.AddDays(30),SourceUrl="https://example.com/confirmed"},hours);
    [Fact]public void UnknownCostsAreNeverZero(){Assert.Null(VisitRecommendationService.Cost(5,null,5,0).Total);Assert.Equal(10,VisitRecommendationService.Cost(5,null,5,0).KnownSubtotal);Assert.Equal(10,VisitRecommendationService.Cost(5,0,5,0).Total);Assert.Throws<ArgumentException>(()=>VisitRecommendationService.Cost(-1,0,0,0));}
    [Fact]public void UnknownAndExpiredHoursStayUnknown(){Assert.Equal("unknown",VisitRecommendationService.Evaluate(new(null,[]),Now,Now).State);var s=Schedule(new VisitHours{Weekday=0,Is24h=true});Assert.Equal("unknown",VisitRecommendationService.Evaluate(s,Now.AddDays(31),Now).State);}
    [Fact]public void OvernightAndCalendarExceptionAreRespected(){var s=Schedule(new VisitHours{Weekday=6,OpenMinute=1200,CloseMinute=120},new VisitHours{Weekday=0,IsClosed=true});var when=new DateTime(2026,10,18,4,0,0,DateTimeKind.Utc);Assert.Equal("open",VisitRecommendationService.Evaluate(s,when,Now).State);s=s with{Hours=[..s.Hours,new(){Weekday=0,SpecialDate=new(2026,10,18),IsClosed=true}]};Assert.Equal("closed",VisitRecommendationService.Evaluate(s,when,Now).State);}
    [Fact]public void SectorAndLastEntryAreNotConfused(){var s=Schedule(new VisitHours{Weekday=0,Is24h=true});Assert.Equal("unknown",VisitRecommendationService.Evaluate(s,Now,Now,"restaurante").State);s.Info!.LastEntryMinutes=11*60;Assert.Equal("closed",VisitRecommendationService.Evaluate(s,Now,Now).State);}
    [Fact]public void FareRequiresSourceAndCurrentVerification(){var line=new TransitLine{Fare=4.60m};Assert.Null(line.ConfirmedFare);line.SourceUrl="https://example.com";line.VerifiedAt=DateTime.UtcNow.AddDays(-1);line.ValidUntil=DateTime.UtcNow.AddDays(2);line.FareValidUntil=DateTime.UtcNow.AddDays(-1);Assert.Null(line.ConfirmedFare);line.FareValidUntil=DateTime.UtcNow.AddDays(1);Assert.Equal(4.60m,line.ConfirmedFare);Assert.False(new TransitLine().HasWheelchairLift);Assert.Equal("",new RiverRoute().OperatingHours);}
    [Fact]public void ProviderParsesOnlyReturnedFactsAndRejectsUnknownWalkDistance()
    {
        using var json=JsonDocument.Parse("""{"routes":[{"distanceMeters":1000,"duration":"600s","polyline":{"encodedPolyline":"official"},"legs":[{"steps":[{"travelMode":"WALK","distanceMeters":100},{"travelMode":"TRANSIT","transitDetails":{"transitLine":{"nameShort":"TEST"},"stopDetails":{"departureStop":{"name":"A"},"arrivalStop":{"name":"B"}}}}]}]}]}""");
        var option=Assert.Single(GoogleRoutePlanner.Parse(json.RootElement,200));Assert.Null(option.Fare);Assert.Equal("official",option.Polyline);Assert.Equal("TEST",option.Steps[1].Line);Assert.Empty(GoogleRoutePlanner.Parse(json.RootElement,50));
        using var missing=JsonDocument.Parse("""{"routes":[{"distanceMeters":100,"duration":"10s","legs":[{"steps":[{"travelMode":"WALK"}]}]}]}""");Assert.Empty(GoogleRoutePlanner.Parse(missing.RootElement,1000));
    }
    [Theory][InlineData("pt-BR")][InlineData("en")][InlineData("fr")][InlineData("es")][InlineData("ru")][InlineData("zh-Hans")][InlineData("ja")]
    public void RoutingIsLocalized(string culture){foreach(var key in new[]{"RouteTitle","RouteRealtimeUnavailable","RouteIncompleteCost","VisitState_unknown"})Assert.NotEqual(key,LocalizationCatalog.Get(key,culture));}
    [MySqlFact]public async Task SchedulesAreAuditedConcurrentChangesBlockedAndPublicRoutesRender()
    {
        var (editor,actor)=await new OperationalTests(fixture).Login();using(editor)using(var scope=fixture.Factory.Services.CreateScope())
        {
            var places=scope.ServiceProvider.GetRequiredService<IPlaceRepository>();var visits=scope.ServiceProvider.GetRequiredService<IVisitRepository>();
            var id=await places.UpsertAsync(new Place{GooglePlaceId="test-"+Guid.NewGuid(),Name="Local fixture autorizado",Municipality="Belém",FormattedAddress="Endereço fixture",IsPublished=true,CachedAt=DateTime.UtcNow,PurgeAfter=DateTime.UtcNow.AddDays(7)},default);
            var info=new PlaceVisitInfo{PlaceId=id,ConfirmedAt=DateTime.UtcNow.AddHours(-1),ValidUntil=DateTime.UtcNow.AddDays(7),SourceUrl="https://example.com/fixture",EntryPrice=0};
            var hours=Enumerable.Range(0,7).Select(d=>new VisitHours{Weekday=d,Is24h=true}).ToArray();
            Assert.True(await visits.SaveAsync(info,hours,0,actor,default));Assert.False(await visits.SaveAsync(info,hours,0,actor,default));
            Assert.Equal(1,(await visits.GetAsync(id,default)).Info!.Revision);
            foreach(var culture in SupportedCultures.All){
                editor.DefaultRequestHeaders.AcceptLanguage.Clear();editor.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
                foreach(var (name,url) in new[]{("visit-place","/como-chegar?placeId="+id),("admin-visit-hours",$"/admin/lugares/{id}/visita")}){
                    var pageResponse=await editor.GetAsync(url);Assert.Equal(HttpStatusCode.OK,pageResponse.StatusCode);
                    if(Environment.GetEnvironmentVariable("MINHABELEM_VISUAL_SNAPSHOTS") is {Length:>0} output){var directory=Path.Combine(output,culture);Directory.CreateDirectory(directory);var html=await pageResponse.Content.ReadAsStringAsync();html=System.Text.RegularExpressions.Regex.Replace(html,"<input[^>]*name=\"__RequestVerificationToken\"[^>]*>","");await File.WriteAllTextAsync(Path.Combine(directory,name+".html"),html);}
                }
            }
            await using var db=await fixture.OpenAsync();Assert.Equal(1,await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_place_visit_history WHERE place_id=@id",new{id}));
            var response=await PortalFixture.Post(editor,"/mobilidade/como-chegar","/como-chegar",new(){{"PlaceId",id.ToString()},{"Origin","Pedreira, Belém"},{"Destination","Ver-o-Peso, Belém"},{"Mode","transit"}});Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Contains("maps",await response.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.BadRequest,(await editor.PostAsync("/mobilidade/como-chegar",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        }
    }
    [MySqlFact]public async Task ExpiredAndFutureAlertsAreNotActive()
    {
        using var scope=fixture.Factory.Services.CreateScope();var repo=scope.ServiceProvider.GetRequiredService<ITransitRepository>();
        var expired=await repo.UpsertAlertAsync(new(){Title="Expired fixture",Description="Teste",StartAt=DateTime.UtcNow.AddDays(-2),EndAt=DateTime.UtcNow.AddDays(-1)},default);
        var future=await repo.UpsertAlertAsync(new(){Title="Future fixture",Description="Teste",StartAt=DateTime.UtcNow.AddDays(1)},default);
        Assert.DoesNotContain(await repo.GetActiveAlertsAsync(default),x=>x.Id==expired||x.Id==future);
    }
    [MySqlFact]public async Task EqualNamesAndEmptySlugsNeverMergeDifferentProviderIds()
    {
        using var scope=fixture.Factory.Services.CreateScope();var repo=scope.ServiceProvider.GetRequiredService<IPlaceRepository>();
        var a=new Place{GooglePlaceId="fixture-"+Guid.NewGuid(),Name="Nomes iguais",PurgeAfter=DateTime.UtcNow.AddDays(1)};
        var b=new Place{GooglePlaceId="fixture-"+Guid.NewGuid(),Name="Nomes iguais",PurgeAfter=DateTime.UtcNow.AddDays(1)};
        var first=await repo.UpsertAsync(a,default);var second=await repo.UpsertAsync(b,default);
        Assert.True(first>0);Assert.True(second>0);Assert.NotEqual(first,second);Assert.NotEqual((await repo.GetAsync(first,default))!.Slug,(await repo.GetAsync(second,default))!.Slug);
        var original=a.Slug;a.Name="Nome atualizado";Assert.Equal(first,await repo.UpsertAsync(a,default));Assert.Equal(original,(await repo.GetAsync(first,default))!.Slug);
    }
    [MySqlFact]public async Task AssistantCannotExposeUnpublishedOrUnverifiedContent()
    {
        using var scope=fixture.Factory.Services.CreateScope();var repo=scope.ServiceProvider.GetRequiredService<IContentRepository>();var marker="Fonte"+Guid.NewGuid().ToString("N");
        var allowed=new ContentItem{Title=marker,Summary="Informação de fixture",Slug=Guid.NewGuid().ToString("N"),SourceUrl="https://example.com/"+Guid.NewGuid(),SourceName="Fixture",Status=ContentStatus.Published,VerificationStatus=VerificationStatus.Verified};
        var denied=new ContentItem{Title=marker,Summary="Rascunho reservado",Slug=Guid.NewGuid().ToString("N"),SourceUrl="https://example.com/"+Guid.NewGuid(),SourceName="Fixture",Status=ContentStatus.Draft};
        await repo.InsertAsync(allowed,null,"Fixture",default);await repo.InsertAsync(denied,null,"Fixture",default);
        var answer=await scope.ServiceProvider.GetRequiredService<ITouristAssistant>().SearchAsync(marker,"pt-BR",true,default);
        Assert.Contains(answer.Content,x=>x.Id==allowed.Id);Assert.DoesNotContain(answer.Content,x=>x.Id==denied.Id);
    }
    [MySqlFact]public async Task ItineraryExcludesClosedPlacesAndDoesNotConfirmBudgetWithoutReturnCost()
    {
        var(client,actor)=await new OperationalTests(fixture).Login();using(client)using(var scope=fixture.Factory.Services.CreateScope())
        {
            var places=scope.ServiceProvider.GetRequiredService<IPlaceRepository>();var visits=scope.ServiceProvider.GetRequiredService<IVisitRepository>();long openId=0;
            for(var i=0;i<2;i++)
            {
                var id=await places.UpsertAsync(new(){GooglePlaceId="fixture-"+Guid.NewGuid(),Name="Planejamento fixture "+i,IsPublished=true,PrimaryType="museum",Latitude=-1.45,Longitude=-48.49,FormattedAddress="Endereço fixture "+i,PurgeAfter=DateTime.UtcNow.AddDays(7)},default);if(i==0)openId=id;
                Assert.True(await visits.SaveAsync(new(){PlaceId=id,ConfirmedAt=DateTime.UtcNow.AddHours(-1),ValidUntil=DateTime.UtcNow.AddDays(7),SourceUrl="https://example.com/horarios",EntryPrice=0},Enumerable.Range(0,7).Select(d=>new VisitHours{Weekday=d,Is24h=i==0,IsClosed=i==1}).ToArray(),0,actor,default));
            }
            var planner=new MinhaBelem.Infrastructure.Places.ItineraryPlanning(places,visits,new FixtureRoutePlanner());var request=new ItineraryRequest{Origin="Origem fixture",LocalStart=MinhaBelem.Web.Models.PortalTime.Local(DateTime.UtcNow).Date.AddDays(1).AddHours(9),Category="museum",Days=.5m,VisitMinutes=90};
            var result=await planner.PlanAsync(request,"pt-BR",default);Assert.Equal("calculated",result.State);Assert.Contains(result.Days.SelectMany(d=>d.Visits),v=>v.Place.Id==openId);Assert.Equal(4,Assert.Single(result.Days).KnownCost);
            request.Budget=3;var limited=await planner.PlanAsync(request,"pt-BR",default);Assert.Equal("unavailable",limited.State);Assert.Empty(limited.Days.SelectMany(d=>d.Visits));
        }
    }
    private sealed class FixtureRoutePlanner:IRoutePlanner{public Task<RoutePlan> PlanAsync(RouteQuery query,CancellationToken ct)=>Task.FromResult(new RoutePlan("available","fixture","Fixture autorizada",DateTime.UtcNow,[new("Fixture",1000,600,[new("TRANSIT","Trecho de teste",1000,"TEST","A","B",null,null)],2,"BRL",null)],"https://example.com/rota"));}
}
