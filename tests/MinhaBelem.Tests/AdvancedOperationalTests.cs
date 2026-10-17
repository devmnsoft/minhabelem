using System.Net;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Infrastructure.Ai;
using MinhaBelem.Infrastructure.Identity;
using MinhaBelem.Infrastructure.MySql;
using MinhaBelem.Infrastructure.Networking;

namespace MinhaBelem.Tests;
[Collection("MySql")]
public sealed class AdvancedOperationalTests(PortalFixture fixture)
{
    [MySqlFact]
    public async Task ExpirationPreservesSourceAndWritesRevisionAndAudit()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var sourceId = await scope.ServiceProvider.GetRequiredService<IIngestionRepository>().UpsertSourceAsync(new IngestionSource { Name = "Fonte de validade", FeedUrl = "https://example.com/" + Guid.NewGuid().ToString("N") }, default);
        var repo = scope.ServiceProvider.GetRequiredService<IContentRepository>();
        var item = new ContentItem { Title = "Aviso expirado", Summary = "Informação com validade explícita", SourceId = sourceId, SourceName = "Fonte de validade", SourceUrl = "https://example.com/" + Guid.NewGuid().ToString("N"), Slug = Guid.NewGuid().ToString("N"), Status = ContentStatus.Published, VerificationStatus = VerificationStatus.Verified, ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        var id = await repo.InsertAsync(item, null, "Fixture de expiração", default);
        Assert.Equal(sourceId, (await repo.GetAsync(id, default))!.SourceId);
        Assert.True(await repo.ExpireOldContentAsync(DateTime.UtcNow, default) >= 1);
        var archived = (await repo.GetAsync(id, default))!;
        Assert.Equal(ContentStatus.Archived, archived.Status);
        Assert.Equal(sourceId, archived.SourceId);
        Assert.Equal(2, archived.Revision);
        Assert.Contains(await repo.RevisionsAsync(id, default), x => x.ChangeSummary!.Contains("validade encerrado"));
        Assert.Contains(await scope.ServiceProvider.GetRequiredService<IAuditRepository>().RecentAsync(100, default), x => x.EntityId == id && x.Action == "content.revised" && x.ActorId is null);
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Home/Detalhe/{id}")).StatusCode);
    }

    [MySqlFact]
    public async Task PlacesReadsBooleanAccessibilityWithoutCreatingAPersistentCache()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var handler = new DiscoveryTests.JsonHandler("""{"id":"place-accessibility","displayName":{"text":"Local acessível"},"accessibilityOptions":{"wheelchairAccessibleEntrance":true}}""");
        var client = new MinhaBelem.Infrastructure.Places.GooglePlacesClient(new DiscoveryTests.Factory(handler), new() { ApiKey = "fixture-only", DailyRequestLimit = 100 }, NullLogger<MinhaBelem.Infrastructure.Places.GooglePlacesClient>.Instance, scope.ServiceProvider.GetRequiredService<ApiBudget>());
        var result = await client.GetDetailsAsync("place-accessibility", default);
        Assert.NotNull(result);
        Assert.True(result.WheelchairAccessible);
        Assert.Equal(result.CachedAt, result.PurgeAfter);
        Assert.DoesNotContain("fixture-only", handler.Url);
    }

    [MySqlFact]
    public async Task RoleRevocationsSurviveInstallerReexecutionAndInvalidateSessions()
    {
        var (moderator, moderatorId) = await new OperationalTests(fixture).Login(MinhaBelem.Core.Security.Roles.Moderator); using var target = moderator;
        var (adminClient, actor) = await new OperationalTests(fixture).Login(); using var owner = adminClient;
        using var scope = fixture.Factory.Services.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<IAdministrationRepository>();
        try
        {
            var response = await PortalFixture.Post(adminClient, "/admin/perfis", "/admin/perfis", new() { ["role"] = "Moderator", ["permissions[0]"] = "content.read" });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await moderator.GetAsync("/editorial")).StatusCode);
            using var stream = typeof(SchemaMigrator).Assembly.GetManifestResourceStream("MinhaBelem.Infrastructure.MINHABELEM.SQL")!;
            using var reader = new StreamReader(stream);
            await using var db = await fixture.OpenAsync(); await db.ExecuteAsync(await reader.ReadToEndAsync());
            var permissions = await admin.PermissionsAsync(moderatorId, default);
            Assert.Contains("content.read", permissions); Assert.DoesNotContain("candidate.review", permissions);
        }
        finally { await admin.SetRolePermissionsAsync("Moderator", MinhaBelem.Core.Security.Permissions.ByRole["Moderator"], actor, default); }
    }
    [MySqlFact]
    public async Task SensitivePublicationRequiresAnExplicitHumanAcknowledgement()
    {
        var (client, actor) = await new OperationalTests(fixture).Login(); using var owner = client;
        using var scope = fixture.Factory.Services.CreateScope(); var repo = scope.ServiceProvider.GetRequiredService<IContentRepository>();
        var item = new ContentItem { Title = "Pauta política de teste", Summary = "Informações verificadas para teste", AuthorName = "Equipe editorial", SourceName = "Fonte identificada", SourceUrl = "https://example.com/" + Guid.NewGuid().ToString("N"), Slug = Guid.NewGuid().ToString("N"), Category = ContentCategory.Politica, Status = ContentStatus.InReview, VerificationStatus = VerificationStatus.Verified, Flags = ModerationFlag.Sensitive };
        var id = await repo.InsertAsync(item, actor, "Revisão humana", default);
        await PortalFixture.Post(client, $"/editorial/publicar/{id}", $"/editorial/editar/{id}", new());
        Assert.Equal(ContentStatus.InReview, (await repo.GetAsync(id, default))!.Status);
        await PortalFixture.Post(client, $"/editorial/publicar/{id}", $"/editorial/editar/{id}", new() { ["confirmSensitive"] = "true" });
        Assert.Equal(ContentStatus.Published, (await repo.GetAsync(id, default))!.Status);
    }
    [MySqlFact]
    public async Task CasesInvolvingChildrenAreHeldBeforeCallingExternalAi()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IAiAnalysisService>().AnalyzeAsync(new("Caso de criança desaparecida", "Dados pessoais não devem seguir para serviços externos", "https://example.com", Array.Empty<ContentItem>()), default);
        Assert.Empty(result.Runs); Assert.True(result.Safety!.IsSensitive);
        Assert.True(result.Classification!.Flags.HasFlag(ModerationFlag.Sensitive));
    }
    [MySqlFact]
    public async Task RecurringInstancesKeepSeparateIdentitiesAfterEditing()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionRepository>();
        var source = new IngestionSource { Name = "Calendário próprio", FeedUrl = "https://example.com/" + Guid.NewGuid().ToString("N"), LicenseNote = "Fixture" }; source.Id = await ingestion.UpsertSourceAsync(source, default);
        var run = await ingestion.StartRunAsync(default); var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryRepository>();
        var first = await discovery.StoreAsync(source, new("Evento recorrente", "Programação", source.FeedUrl + "/evento", "Organizador", null, new(DateTime.UtcNow.AddDays(30), null, "Belém", null, null, null, false, null, null, source.FeedUrl), "edicao-1"), run, default);
        var second = await discovery.StoreAsync(source, new("Evento recorrente", "Programação", source.FeedUrl + "/evento", "Organizador", null, new(DateTime.UtcNow.AddDays(60), null, "Belém", null, null, null, false, null, null, source.FeedUrl), "edicao-2"), run, default);
        var workflow = scope.ServiceProvider.GetRequiredService<IEditorialWorkflow>(); var repo = scope.ServiceProvider.GetRequiredService<IContentRepository>();
        var id1 = (await workflow.ImportAsync(first.Id, null, default))!.Value; var id2 = (await workflow.ImportAsync(second.Id, null, default))!.Value;
        var one = (await repo.GetAsync(id1, default))!; var two = (await repo.GetAsync(id2, default))!;
        Assert.NotEqual(Convert.ToHexString(one.SourceUrlHash!), Convert.ToHexString(two.SourceUrlHash!));
        Assert.True(await repo.UpdateAsync(one, null, "Edição 1", default)); Assert.True(await repo.UpdateAsync(two, null, "Edição 2", default));
    }
    [MySqlFact]
    public async Task AiFallbackIsLimitedAndPersistsFailedAndSuccessfulAttempts()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var logger = new AiRunLogger(scope.ServiceProvider.GetRequiredService<IAiRunRepository>(), NullLogger<AiRunLogger>.Instance);
        var service = new AiAnalysisService(logger, new FallbackResolver(), new AiOptions(), NullLogger<AiAnalysisService>.Instance);
        var result = await service.AnalyzeAsync(new("Evento em Belém", "Programa cultural confirmado", "https://example.com", Array.Empty<ContentItem>()), default);
        Assert.Equal(2, result.Runs.Count); Assert.False(result.Runs[0].Success); Assert.True(result.Runs[1].Success); Assert.NotNull(result.Classification);
    }
    private sealed class FallbackResolver : IAiClientResolver
    {
        public IAiCompletionClient Resolve(AiOptions options) => new FailedClient();
        public IReadOnlyList<IAiCompletionClient> Candidates(AiOptions options) => new[] { new FailedClient(), new SampleAi().Resolve(options) };
        private sealed class FailedClient : IAiCompletionClient
        {
            public AiProvider Provider => AiProvider.DeepSeek; public bool IsConfigured => true; public string Model => "fixture-error";
            public Task<AiCompletion> CompleteAsync(string system, string user, CancellationToken ct) => Task.FromException<AiCompletion>(new HttpRequestException("Fixture de indisponibilidade", null, HttpStatusCode.ServiceUnavailable));
        }
    }
    [MySqlFact]
    public async Task EntireSingleSqlRunsTwiceWithoutDataLossOrDuplicatedSeeds()
    {
        await using var db = await fixture.OpenAsync();
        var id = await db.ExecuteScalarAsync<long>("INSERT INTO mb_community_submissions (display_name,message) VALUES ('Teste SQL','Preservar registro'); SELECT LAST_INSERT_ID();");
        using var stream = typeof(SchemaMigrator).Assembly.GetManifestResourceStream("MinhaBelem.Infrastructure.MINHABELEM.SQL")!;
        using var reader = new StreamReader(stream); var sql = await reader.ReadToEndAsync();
        await db.ExecuteAsync(sql); await db.ExecuteAsync(sql);
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_community_submissions WHERE id=@id", new { id }));
        Assert.Equal(4, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_roles"));
        Assert.Equal(17, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_permissions"));
        Assert.Equal(38, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_role_permissions"));
        Assert.Equal(60, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE()"));
    }
    [MySqlFact]
    public async Task LegacyImportIsExplicitAdditiveAndKeepsPublicIds()
    {
        await using var db = await fixture.OpenAsync();
        var url = "https://example.com/legado-" + Guid.NewGuid().ToString("N");
        await db.ExecuteAsync("INSERT INTO content_items (id,category,title,summary,source_name,source_url,source_url_hash,status,verification_status) VALUES (999990,'cultura','Conteúdo legado preservado','Resumo original','Fonte legada',@url,@hash,'published','verified')", new { url, hash = MinhaBelem.Core.Text.Hashing.UrlHash(url) });
        using var scope = fixture.Factory.Services.CreateScope();
        var importer = scope.ServiceProvider.GetRequiredService<LegacyDataImporter>();
        await importer.ImportAsync(); await importer.ImportAsync();
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM content_items WHERE id=999990"));
        var item = await scope.ServiceProvider.GetRequiredService<IContentRepository>().GetAsync(999990, default);
        Assert.Equal("Conteúdo legado preservado", item!.Title);
        Assert.Equal(ContentStatus.Published, item.Status);
        Assert.Equal(1, await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM mb_content_items WHERE source_url=@url", new { url }));
    }
    [MySqlFact]
    public async Task ChangedSourceCreatesProposalWithoutOverwritingPublishedEvent()
    {
        var (client, actor) = await new OperationalTests(fixture).Login(); using var owner = client;
        using var scope = fixture.Factory.Services.CreateScope();
        var ingestion = scope.ServiceProvider.GetRequiredService<IIngestionRepository>();
        var source = new IngestionSource { Name = "Organizador", FeedUrl = "https://example.com/" + Guid.NewGuid().ToString("N"), Enabled = true, Approved = true, LicenseNote = "Fixture" };
        source.Id = await ingestion.UpsertSourceAsync(source, default);
        var run = await ingestion.StartRunAsync(default);
        var discovery = scope.ServiceProvider.GetRequiredService<IDiscoveryRepository>();
        var start = DateTime.UtcNow.AddDays(40);
        var evidence = new EventEvidence(start, start.AddHours(2), "Belém, Pará", "Organizador", "Scheduled", "0", true, "0", null, source.FeedUrl);
        var stored = await discovery.StoreAsync(source, new("Festival original", "Resumo original", source.FeedUrl + "/evento", "Organização", DateTime.UtcNow, evidence), run, default);
        var id = (await scope.ServiceProvider.GetRequiredService<IEditorialWorkflow>().ImportAsync(stored.Id, actor, default))!.Value;
        var repo = scope.ServiceProvider.GetRequiredService<IContentRepository>();
        var item = (await repo.GetAsync(id, default))!; item.VerificationStatus = VerificationStatus.Verified; item.Kind = ContentKind.Event; item.EventStartAt = start; item.EventDetails = evidence;
        await repo.UpdateAsync(item, actor, "Conferido", default); Assert.True(await repo.SetStatusAsync(id, ContentStatus.Published, actor, default));
        var changed = await discovery.StoreAsync(source, new("Nova programação", "Horário alterado", source.FeedUrl + "/evento", "Organização", DateTime.UtcNow, evidence with { StartUtc = start.AddDays(1) }), run, default);
        Assert.Equal(stored.Id, changed.Id); Assert.True(changed.Changed);
        Assert.Equal(ContentStatus.Published, (await repo.GetAsync(id, default))!.Status);
        var candidate = (await ingestion.CandidateAsync(stored.Id, default))!;
        Assert.Contains("Nova programa", System.Net.WebUtility.HtmlDecode(await client.GetStringAsync($"/editorial/comparar/{stored.Id}")));
        var result = await PortalFixture.Post(client, $"/editorial/atualizacao/{stored.Id}", $"/editorial/comparar/{stored.Id}", new() { ["revision"] = (await repo.GetAsync(id, default))!.Revision.ToString(), ["candidateTicks"] = candidate.UpdatedAt.Ticks.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Equal(ContentStatus.InReview, (await repo.GetAsync(id, default))!.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Home/Detalhe/{id}")).StatusCode);
    }
    [MySqlFact]
    public async Task FutureAgendaFiltersAndPublishedCancellationWorkOverHttp()
    {
        var (client, actor) = await new OperationalTests(fixture).Login(); using var owner = client;
        using var scope = fixture.Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IContentRepository>();
        var title = "Teatro infantil " + Guid.NewGuid().ToString("N");
        var item = new ContentItem { Title = title, Summary = "Evento confirmado com organizador", AuthorName = "Organização", SourceName = "Fonte autorizada", SourceUrl = "https://example.com/" + Guid.NewGuid().ToString("N"), Slug = Guid.NewGuid().ToString("N"), Kind = ContentKind.Event, EventStartAt = DateTime.UtcNow.AddDays(40), Category = ContentCategory.Cultura, Subject = "teatro", IsFree = true, Status = ContentStatus.InReview, VerificationStatus = VerificationStatus.Verified, Audiences = new[] { Audience.Infantil }, Accessibility = new[] { AccessibilityFeature.Fisica }, EventDetails = new EventEvidence(DateTime.UtcNow.AddDays(40), null, "Nazaré, Belém", "Organizador", "Scheduled", "0", true, "0", null, "https://example.com") };
        var id = await repo.InsertAsync(item, actor, "Agenda de teste", default);
        Assert.True(await repo.SetStatusAsync(id, ContentStatus.Published, actor, default));
        Assert.Contains(title, await client.GetStringAsync("/agenda?dias=365"));
        Assert.DoesNotContain(title, await client.GetStringAsync("/agenda?dias=7"));
        Assert.Contains(title, await client.GetStringAsync("/programacao-infantil?dias=365"));
        foreach (var page in new[] { "/agenda/semana", "/proximos-eventos", "/cinema-teatro", "/esporte-lazer", "/eventos-gratuitos", "/meu-bairro", "/fim-de-semana", "/eventos-do-para", "/feriados", "/agora" }) Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(page)).StatusCode);
        await PortalFixture.Post(client, $"/editorial/situacao/{id}", $"/editorial/editar/{id}", new() { ["status"] = "Cancelled" });
        Assert.Contains("Evento cancelado", await client.GetStringAsync($"/Home/Detalhe/{id}"));
    }
    [MySqlFact]
    public async Task ApiBudgetIsAtomicAndGatewayRecordsActualUsageShape()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var budget = scope.ServiceProvider.GetRequiredService<ApiBudget>();
        var service = "test-" + Guid.NewGuid().ToString("N");
        var reservations = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => budget.ReserveAsync(service, 3, default)));
        Assert.Equal(3, reservations.Count(x => x));
        var options = new AiOptions { MaxDailyRequests = 100, DeepSeek = new AiProviderOptions { ApiKey = "fixture-not-a-secret", Model = "fixture", InputCostPerMillion = 1, OutputCostPerMillion = 2 } };
        var handler = new DiscoveryTests.JsonHandler("""{"model":"fixture-v1","choices":[{"message":{"content":"{\"categoria\":\"cultura\"}"}}],"usage":{"prompt_tokens":10,"completion_tokens":20}}""");
        var gateway = new OpenAiCompatibleClient(new DiscoveryTests.Factory(handler), options, NullLogger<OpenAiCompatibleClient>.Instance, budget, AiProvider.DeepSeek);
        var result = await gateway.CompleteAsync("Sistema", "Conteúdo", default);
        Assert.Equal(0.00005m, result.CostUsd); Assert.Equal(10, result.PromptTokens); Assert.Equal("fixture-v1", result.ModelVersion);
        Assert.DoesNotContain("fixture-not-a-secret", handler.Url);
    }
    [MySqlFact]
    public async Task RecoveryCodesAreEncryptedAndSingleUse()
    {
        var (client, id) = await new OperationalTests(fixture).Login(); using var owner = client;
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(id.ToString()))!;
        var codes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 2))!.ToArray();
        var store = (IUserTwoFactorRecoveryCodeStore<ApplicationUser>)scope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
        Assert.True(await store.RedeemCodeAsync(user, codes[0], default));
        Assert.False(await store.RedeemCodeAsync(user, codes[0], default));
        Assert.Equal(1, await store.CountCodesAsync(user, default));
        await using var db = await fixture.OpenAsync();
        var encrypted = await db.ExecuteScalarAsync<string>("SELECT value_cipher FROM mb_user_tokens WHERE user_id=@id AND name='RecoveryCodes'", new { id });
        Assert.DoesNotContain(codes[1], encrypted);
    }
    [MySqlFact]
    public async Task SuspendingUserInvalidatesTheirNextRequest()
    {
        var (editor, editorId) = await new OperationalTests(fixture).Login(MinhaBelem.Core.Security.Roles.Editor); using var target = editor;
        var (admin, _) = await new OperationalTests(fixture).Login(); using var owner = admin;
        var response = await PortalFixture.Post(admin, $"/admin/usuarios/{editorId}/estado", "/admin/usuarios", new() { ["enabled"] = "false" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await editor.GetAsync("/editorial")).StatusCode);
    }
    [MySqlFact]
    public async Task SourceQuotaAndDisabledOcrDoNotPretendSuccessfulExecution()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var source = await scope.ServiceProvider.GetRequiredService<IIngestionRepository>().UpsertSourceAsync(new IngestionSource { Name = "Teste de cota", FeedUrl = "https://example.com/" + Guid.NewGuid().ToString("N") }, default);
        var repository = scope.ServiceProvider.GetRequiredService<IDiscoveryRepository>();
        Assert.True(await repository.ReserveRequestAsync(source, 1, default));
        Assert.False(await repository.ReserveRequestAsync(source, 1, default));
        var media = scope.ServiceProvider.GetRequiredService<IMediaAnalysis>();
        Assert.False(media.IsConfigured);
        await Assert.ThrowsAsync<InvalidOperationException>(() => media.AnalyzeAsync(1, 1, default));
    }
}
