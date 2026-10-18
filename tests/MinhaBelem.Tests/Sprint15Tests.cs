using System.Net;
using System.Security.Claims;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Infrastructure.Identity;
using MinhaBelem.Web.Resources;
namespace MinhaBelem.Tests;

[Collection("MySql")]
public sealed class Sprint15Tests(PortalFixture fixture)
{
    [MySqlFact]
    public async Task MandatoryPasswordChangeRejectsReuseAndRequiresActualRotation()
    {
        using var client = fixture.Client();
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var password = Guid.NewGuid().ToString("N") + "Aa1!";
        var replacement = Guid.NewGuid().ToString("N") + "Bb2!";
        var email = Guid.NewGuid().ToString("N") + "@example.com";
        var user = new ApplicationUser { Email = email, UserName = email, EmailConfirmed = true, Enabled = true, MustChangePassword = true };
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, Roles.Editor)).Succeeded);
        Assert.Equal(HttpStatusCode.Redirect, (await PortalFixture.Post(client, "/conta/login", "/conta/login", new() { ["Email"] = email, ["Password"] = password })).StatusCode);
        Assert.EndsWith("/conta/perfil", (await client.GetAsync("/editorial")).Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect, (await PortalFixture.Post(client, "/conta/senha", "/conta/perfil", new() { ["CurrentPassword"] = password, ["NewPassword"] = password })).StatusCode);
        Assert.True((await users.FindByIdAsync(user.Id.ToString()))!.MustChangePassword);
        var changed = await PortalFixture.Post(client, "/conta/senha", "/conta/perfil", new() { ["CurrentPassword"] = password, ["NewPassword"] = replacement });
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.EndsWith("/conta/login", changed.Headers.Location!.ToString());
        var updated = (await users.FindByIdAsync(user.Id.ToString()))!;
        Assert.False(updated.MustChangePassword);
        Assert.False(await users.CheckPasswordAsync(updated, password));
        Assert.True(await users.CheckPasswordAsync(updated, replacement));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/conta/perfil")).StatusCode);
    }
    [Theory]
    [InlineData("pt-BR")][InlineData("en")][InlineData("fr")][InlineData("es")][InlineData("ru")][InlineData("zh-Hans")][InlineData("ja")]
    public void BatchMessagesAreLocalized(string culture)
    {
        foreach (var key in new[] { "BatchTitle", "BatchSafety", "BatchPreview", "BatchConfirmation", "BatchResult" }) Assert.NotEqual(key, LocalizationCatalog.Get(key, culture));
    }
    private static ClaimsPrincipal Actor(long id, string permission) => new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim("amr", "mfa"), new Claim(PermissionClaimsPrincipalFactory.PermissionClaimType, permission) }, "test"));
    [MySqlFact]
    public async Task BatchIsDurableIdempotentAndIsolatesConflictsAndSensitiveItems()
    {
        var (client, actorId) = await new OperationalTests(fixture).Login(Roles.Editor);
        using (client)
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEditorialBatches>();
            var repo = scope.ServiceProvider.GetRequiredService<IContentRepository>();
            var items = new List<ContentItem>();
            for (var i=0; i<3; i++)
            {
                var item = new ContentItem { Title = "Fixture editorial " + i, Summary = "Conteúdo de teste autorizado", SourceName = "Fixture", SourceUrl = "https://example.com/" + Guid.NewGuid(), Slug = Guid.NewGuid().ToString("N"), AuthorName = "Editor", Status = ContentStatus.InReview, VerificationStatus = VerificationStatus.Verified, Flags = i == 1 ? ModerationFlag.Sensitive : ModerationFlag.None };
                await repo.InsertAsync(item, actorId, "Teste", default); items.Add(item);
            }
            var principal = Actor(actorId, Permissions.ContentPublish);
            var id = await service.PreviewAsync(new() { Action="publish", Reason="Conferência editorial de homologação", Targets=items.Select(x=>new BatchTarget(x.Id,"content",x.Revision)).ToArray() }, principal, default);
            Assert.Equal("preview", (await service.GetAsync(id,actorId,default))!.Status);
            Assert.False(await service.ProcessNextAsync(default));
            items[2].Title="Alterado por outro editor"; Assert.True(await repo.UpdateAsync(items[2],actorId,"Concorrência",default));
            Assert.True(await service.ConfirmAsync(id,principal,default)); Assert.True(await service.ConfirmAsync(id,principal,default));
            Assert.True(await service.ProcessNextAsync(default));
            var batch = (await service.GetAsync(id,actorId,default))!;
            Assert.Single(batch.Items,x=>x.Status=="completed"); Assert.Equal(2,batch.Items.Count(x=>x.Status=="blocked"));
            Assert.Equal(ContentStatus.Published,(await repo.GetAsync(items[0].Id,default))!.Status);
            Assert.Equal(ContentStatus.InReview,(await repo.GetAsync(items[1].Id,default))!.Status);
            Assert.True(await service.ConfirmAsync(id,principal,default)); Assert.False(await service.ProcessNextAsync(default));
            await using var db=await fixture.OpenAsync();
            Assert.Equal(3,await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_audit_logs WHERE action='editorial.batch_item' AND JSON_UNQUOTE(JSON_EXTRACT(details,'$.Id'))=@id",new{id}));
            Assert.Null(await service.GetAsync(id,actorId+99999,default));
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync($"/editorial/lotes/{id}/confirmar",new FormUrlEncodedContent(new Dictionary<string,string>{{"confirmed","true"}}))).StatusCode);
            foreach(var culture in SupportedCultures.All){client.DefaultRequestHeaders.AcceptLanguage.Clear();client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture); Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/editorial/lotes")).StatusCode); Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/editorial/lotes/"+id)).StatusCode);}
        }
    }
    [MySqlFact]
    public async Task RevokedPermissionsBlockQueuedBatchAndModeratorCannotPublish()
    {
        var (client,actorId)=await new OperationalTests(fixture).Login(Roles.Moderator);
        using(client) using(var scope=fixture.Factory.Services.CreateScope())
        {
            var service=scope.ServiceProvider.GetRequiredService<IEditorialBatches>();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.PreviewAsync(new(){Action="publish",Reason="Tentativa sem autorização",Targets=[new(1,"content",1)]},Actor(actorId,Permissions.CandidateReview),default));
        }
    }
    [MySqlFact]public async Task AccountPreferencesPersistWithoutChangingPermissions()
    {
        var(client,actor)=await new OperationalTests(fixture).Login();using(client)
        {
            using var scope=fixture.Factory.Services.CreateScope();var admin=scope.ServiceProvider.GetRequiredService<IAdministrationRepository>();var before=(await admin.PermissionsAsync(actor,default)).Order().ToArray();
            Assert.Equal(HttpStatusCode.Redirect,(await PortalFixture.Post(client,"/conta/preferencias","/conta/perfil",new(){{"culture","fr"},{"accessible","true"}})).StatusCode);
            var userManager=scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();var user=(await userManager.FindByIdAsync(actor.ToString()))!;var claims=await userManager.GetClaimsAsync(user);
            Assert.Contains(claims,c=>c.Type=="mb:preference:culture"&&c.Value=="fr");Assert.Contains(claims,c=>c.Type=="mb:preference:accessible"&&c.Value=="true");Assert.Equal(before,(await admin.PermissionsAsync(actor,default)).Order().ToArray());
        }
    }
    [MySqlFact]public async Task PermissionRevokedAfterConfirmationBlocksWorkerMutation()
    {
        var(client,actor)=await new OperationalTests(fixture).Login(Roles.Editor);using(client)using(var scope=fixture.Factory.Services.CreateScope())
        {
            var repo=scope.ServiceProvider.GetRequiredService<IContentRepository>();var service=scope.ServiceProvider.GetRequiredService<IEditorialBatches>();var principal=Actor(actor,Permissions.ContentWrite);
            var item=new ContentItem{Title="Fixture revogação",Summary="Teste de autorização na execução",Slug=Guid.NewGuid().ToString("N"),SourceName="Fixture",SourceUrl="https://example.com/"+Guid.NewGuid()};await repo.InsertAsync(item,actor,"Fixture",default);
            var id=await service.PreviewAsync(new(){Action="review",Reason="Revisão solicitada antes de revogar acesso",Targets=[new(item.Id,"content",item.Revision)]},principal,default);Assert.True(await service.ConfirmAsync(id,principal,default));
            var manager=scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();var user=(await manager.FindByIdAsync(actor.ToString()))!;Assert.True((await manager.RemoveFromRoleAsync(user,Roles.Editor)).Succeeded);
            Assert.True(await service.ProcessNextAsync(default));Assert.Equal("blocked",Assert.Single((await service.GetAsync(id,actor,default))!.Items).Status);Assert.Equal(ContentStatus.Draft,(await repo.GetAsync(item.Id,default))!.Status);
        }
    }
    [MySqlFact]public async Task HttpSelectionPreviewAndConfirmationChangeOnlySelectedItems()
    {
        var(client,actor)=await new OperationalTests(fixture).Login(Roles.Editor);using(client)using(var scope=fixture.Factory.Services.CreateScope())
        {
            var repo=scope.ServiceProvider.GetRequiredService<IContentRepository>();var items=new List<ContentItem>();
            for(var i=0;i<3;i++){var item=new ContentItem{Title="Seleção HTTP fixture "+i,Summary="Teste de seleção por formulário",Slug=Guid.NewGuid().ToString("N"),SourceName="Fixture",SourceUrl="https://example.com/"+Guid.NewGuid()};await repo.InsertAsync(item,actor,"Fixture",default);items.Add(item);}
            var preview=await PortalFixture.Post(client,"/editorial/lotes/previa","/editorial/lotes",new(){{"Action","review"},{"Reason","Revisão selecionada pelo editor no formulário"},{"kind","content"},{"selected[0]",$"content:{items[0].Id}:{items[0].Revision}"},{"selected[1]",$"content:{items[1].Id}:{items[1].Revision}"}});
            Assert.Equal(HttpStatusCode.Redirect,preview.StatusCode);var location=preview.Headers.Location!.ToString();var id=location.Split('/').Last();
            Assert.Equal(HttpStatusCode.Redirect,(await PortalFixture.Post(client,location+"/confirmar",location,new(){{"confirmed","true"}})).StatusCode);
            Assert.True(await scope.ServiceProvider.GetRequiredService<IEditorialBatches>().ProcessNextAsync(default));
            Assert.Equal(ContentStatus.InReview,(await repo.GetAsync(items[0].Id,default))!.Status);Assert.Equal(ContentStatus.InReview,(await repo.GetAsync(items[1].Id,default))!.Status);Assert.Equal(ContentStatus.Draft,(await repo.GetAsync(items[2].Id,default))!.Status);
            Assert.Equal(2,(await scope.ServiceProvider.GetRequiredService<IEditorialBatches>().GetAsync(id,actor,default))!.Items.Count);
        }
    }
    [MySqlFact]public async Task RadarAndUserFormsHaveAccessibleControlLabels()
    {
        var(client,_)=await new OperationalTests(fixture).Login();using(client)
        foreach(var culture in SupportedCultures.All)
        {
            client.DefaultRequestHeaders.AcceptLanguage.Clear();client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
            foreach(var (name,path) in new[]{("admin-radar","/admin/radar"),("admin-usuarios","/admin/usuarios")})
            {
                var html=await client.GetStringAsync(path);Assert.Contains(name=="admin-radar"?"for=\"radar-Platform\"":"aria-label=",html);
                if(Environment.GetEnvironmentVariable("MINHABELEM_VISUAL_SNAPSHOTS") is {Length:>0} output){var directory=Path.Combine(output,culture);Directory.CreateDirectory(directory);html=System.Text.RegularExpressions.Regex.Replace(html,"<input[^>]*name=\"__RequestVerificationToken\"[^>]*>","");await File.WriteAllTextAsync(Path.Combine(directory,name+".html"),html);}
            }
        }
    }
}

