using System.Net;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Infrastructure.Background;
using MinhaBelem.Infrastructure.Identity;
using MinhaBelem.Infrastructure.MySql;
using MySqlConnector;

namespace MinhaBelem.Tests;

[Collection("MySql")]
public sealed class OperationalTests(PortalFixture fixture)
{
    internal async Task<(HttpClient Client, long Id)> Login(string role = Roles.SuperAdmin, bool setupMfa = true)
    {
        var email = Guid.NewGuid().ToString("N") + "@example.com";
        var password = Guid.NewGuid().ToString("N") + "Aa1!";
        long id;
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Email = email, UserName = email, EmailConfirmed = true, Enabled = true, DisplayName = "Usuário de teste", LockoutEnabled = true };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            id = user.Id;
        }
        var client = fixture.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await PortalFixture.Post(client, "/conta/login", "/conta/login", new() { ["Email"] = email, ["Password"] = password })).StatusCode);
        if (!setupMfa) return (client, id);
        Assert.Equal(HttpStatusCode.Redirect, (await PortalFixture.Post(client, "/conta/mfa/iniciar", "/conta/mfa", new())).StatusCode);
        var html = await client.GetStringAsync("/conta/mfa");
        var secret = Regex.Match(html, "<code>([A-Z2-7]+)</code>").Groups[1].Value;
        Assert.NotEmpty(secret);
        var recovery = await PortalFixture.Post(client, "/conta/mfa", "/conta/mfa", new() { ["code"] = Totp.CurrentCode(secret, DateTime.UtcNow) });
        Assert.Equal(HttpStatusCode.OK, recovery.StatusCode);
        Assert.Contains("recupera", await recovery.Content.ReadAsStringAsync());
        await PortalFixture.Post(client, "/conta/login", "/conta/login", new() { ["Email"] = email, ["Password"] = password });
        Assert.Equal(HttpStatusCode.Redirect, (await PortalFixture.Post(client, "/conta/verificar", "/conta/verificar", new() { ["code"] = Totp.CurrentCode(secret, DateTime.UtcNow) })).StatusCode);
        return (client, id);
    }

    [MySqlFact]
    public async Task IdentityMfaAdministrationAndSessionRevocationWorkOverHttp()
    {
        var (client, id) = await Login();
        using (client)
        {
            foreach (var path in new[] { "/admin", "/admin/fontes", "/admin/usuarios", "/admin/perfis", "/admin/integracoes", "/admin/auditoria", "/admin/configuracoes", "/admin/colaboracoes", "/admin/lugares", "/conta/perfil", "/conta/sessoes", "/editorial" })
            {
                var page = await client.GetAsync(path);
                Assert.True(page.StatusCode == HttpStatusCode.OK, path + " - " + (await page.Content.ReadAsStringAsync()).Split("HEADERS")[0]);
            }
            await using var db = await fixture.OpenAsync();
            var secret = await db.ExecuteScalarAsync<string>("SELECT value_cipher FROM mb_user_tokens WHERE user_id=@id AND name='AuthenticatorKey'", new { id });
            Assert.DoesNotMatch("^[A-Z2-7]{32}$", secret);
            var session = await db.ExecuteScalarAsync<string>("SELECT id FROM mb_user_sessions WHERE user_id=@id AND revoked_at IS NULL ORDER BY created_at DESC LIMIT 1", new { id });
            Assert.NotNull(session);
            await PortalFixture.Post(client, "/conta/sessoes/revogar", "/conta/sessoes", new() { ["id"] = session });
            Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/editorial")).StatusCode);
        }
    }

    [MySqlFact]
    public async Task PasswordOnlyCannotEnterEditorialAndModeratorCannotPublish()
    {
        var (passwordOnly, _) = await Login(setupMfa: false);
        using (passwordOnly)
            Assert.Equal(HttpStatusCode.Redirect, (await passwordOnly.GetAsync("/editorial")).StatusCode); // cookie challenge to access-denied page
        var (moderator, _) = await Login(Roles.Moderator);
        using (moderator)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await moderator.PostAsync("/editorial/publicar/1", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await moderator.GetAsync("/admin/usuarios")).StatusCode);
        }
    }

    [MySqlFact]
    public async Task SourceIngestionAiReviewPublicationUpdateAndArchivePersistEndToEnd()
    {
        var (client, actor) = await Login();
        using (client)
        {
            var url = "https://example.com/" + Guid.NewGuid().ToString("N");
            await PortalFixture.Post(client, "/admin/fontes", "/admin/fontes", new() { ["Name"] = "Fonte autorizada de teste", ["FeedUrl"] = url, ["Protocol"] = "Rss", ["LicenseNote"] = "Fixture autorizada, sem chamadas externas", ["Enabled"] = "true", ["Approved"] = "true" });
            using var scope = fixture.Factory.Services.CreateScope();
            var worker = ActivatorUtilities.CreateInstance<FeedCollectorWorker>(scope.ServiceProvider);
            await worker.CollectOnceAsync(default);
            var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionRepository>();
            var candidate = (await ingestion.CandidatesAsync(new CandidateQuery { PageSize = 100 }, default)).Single(x => x.SourceUrl == url + "/evento");
            Assert.True(candidate.Id > 0);
            var response = await PortalFixture.Post(client, $"/editorial/importar/{candidate.Id}", "/conta/perfil", new());
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var id = (await ingestion.CandidateAsync(candidate.Id, default))!.ContentId!.Value;
            var repo = scope.ServiceProvider.GetRequiredService<IContentRepository>();
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Home/Detalhe/{id}")).StatusCode);
            await PortalFixture.Post(client, $"/editorial/analisar/{id}", $"/editorial/editar/{id}", new());
            var item = (await repo.GetAsync(id, default))!;
            Assert.Equal(MinhaBelem.Core.Taxonomy.ContentCategory.Cultura, item.Category);
            Assert.True(await scope.ServiceProvider.GetRequiredService<IAiRunRepository>().CostSinceAsync(DateTime.UtcNow.AddDays(-1), default) >= 0);
            var fields = new Dictionary<string, string> { ["Id"] = id.ToString(), ["Revision"] = item.Revision.ToString(), ["Title"] = "Festival verificado", ["Summary"] = "Dados conferidos na fonte original.", ["SourceName"] = item.SourceName, ["SourceUrl"] = item.SourceUrl, ["AuthorName"] = "Equipe cultural", ["Category"] = "Cultura", ["Kind"] = "News", ["Verified"] = "true", ["ChangeSummary"] = "Conferência humana na fonte" };
            Assert.Equal(HttpStatusCode.Redirect, (await PortalFixture.Post(client, $"/editorial/editar/{id}", $"/editorial/editar/{id}", fields)).StatusCode);
            await PortalFixture.Post(client, $"/editorial/publicar/{id}", $"/editorial/editar/{id}", new());
            Assert.Equal(ContentStatus.Published, (await repo.GetAsync(id, default))!.Status);
            Assert.Contains("Festival verificado", await client.GetStringAsync($"/Home/Detalhe/{id}"));
            var revision = (await repo.GetAsync(id, default))!.Revision;
            fields["Revision"] = revision.ToString(); fields["Title"] = "Festival atualizado";
            await PortalFixture.Post(client, $"/editorial/editar/{id}", $"/editorial/editar/{id}", fields);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Home/Detalhe/{id}")).StatusCode);
            await PortalFixture.Post(client, $"/editorial/publicar/{id}", $"/editorial/editar/{id}", new());
            await PortalFixture.Post(client, $"/editorial/arquivar/{id}", $"/editorial/editar/{id}", new());
            Assert.Equal(ContentStatus.Archived, (await repo.GetAsync(id, default))!.Status);
            Assert.True((await repo.RevisionsAsync(id, default)).Count >= 6);
            Assert.Contains(await scope.ServiceProvider.GetRequiredService<IAuditRepository>().RecentAsync(100, default), x => x.EntityId == id && x.ActorId == actor && x.Action == "content.revised");
        }
    }

    [MySqlFact]
    public async Task SchemaIsIdempotentAndRevisionConcurrencyIsEnforced()
    {
        await using var data = new MySqlDataSourceBuilder(fixture.ConnectionString).Build();
        var result = await new SchemaMigrator(data, NullLogger<SchemaMigrator>.Instance).ApplyAsync();
        Assert.True(result.AlreadyPresent.Count >= 3);
        using var scope = fixture.Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContentRepository>();
        var item = new ContentItem { Title = "Concorrência", Summary = "Teste de atualização concorrente", SourceName = "Fixture", SourceUrl = "https://example.com/" + Guid.NewGuid().ToString("N"), Slug = Guid.NewGuid().ToString("N") };
        var id = await repo.InsertAsync(item, null, "Inclusão de teste", default);
        Assert.True(id > 0);
        var first = (await repo.GetAsync(id, default))!;
        var second = (await repo.GetAsync(id, default))!;
        first.Title = "Primeiro editor"; second.Title = "Segundo editor";
        Assert.True(await repo.UpdateAsync(first, null, "Primeiro", default));
        Assert.False(await repo.UpdateAsync(second, null, "Segundo", default));
        Assert.Equal("Primeiro editor", (await repo.GetAsync(id, default))!.Title);
    }

    [MySqlFact]
    public async Task CsrfAndCommunityPrivacyAreEnforcedAndGuideUsesInfrastructure()
    {
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Home/Colaborar", new FormUrlEncodedContent(new Dictionary<string,string> { ["nome"] = "Teste", ["mensagem"] = "Sugestão" }))).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPlaceRepository>();
        var id = await repo.UpsertAsync(new Place { GooglePlaceId = Guid.NewGuid().ToString("N"), Name = "Nome editorial próprio", Slug = Guid.NewGuid().ToString("N"), IsPublished = true, PurgeAfter = DateTime.UtcNow.AddDays(1) }, default);
        var html = await client.GetStringAsync($"/guia/{id}");
        Assert.Contains("API simulada", html);
        Assert.Equal("Nome editorial próprio", (await repo.GetAsync(id, default))!.Name);
        Assert.DoesNotContain("test-double", html);
    }
}
