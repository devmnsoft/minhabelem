using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Taxonomy;
using MinhaBelem.Core.Text;
using MinhaBelem.Infrastructure.Ai;
using MinhaBelem.Infrastructure.Background;
using MinhaBelem.Infrastructure.Identity;
using MinhaBelem.Web.Models;
namespace MinhaBelem.Web.Controllers;

[Route("admin"), Authorize]
public sealed class AdminController(IAdministrationRepository admin, IDashboardRepository dashboard, IIngestionRepository ingestion, IAuditRepository audit,
    IAiRunRepository aiRuns, UserManager<ApplicationUser> users, ICommunityRepository community, IPlaceRepository places, IPlacesClient google, IOptions<AiOptions> aiOptions,
    IDiscoveryRepository discovery, ISourceCollection collector, ITranslationRepository translation, IContentTranslationService translationService, IContentRepository content,
    ITransitRepository transit, IAdvertiserRepository advertisers, INeighborhoodRepository neighborhoods) : Controller
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private Task Audit(string action, string type, long? id, CancellationToken ct) => audit.WriteAsync(new AuditEntry { ActorId = Actor, Action = action, EntityType = type, EntityId = id }, ct);
    [HttpGet(""), Authorize(Policy = Permissions.DashboardRead)] public async Task<IActionResult> Index(CancellationToken ct) => View(await dashboard.BuildAsync(ct));
    [HttpGet("perfis"), Authorize(Policy = Permissions.UserManage)]
    public async Task<IActionResult> Perfis(CancellationToken ct) => View(await admin.RolePermissionsAsync(ct));
    [HttpPost("perfis"), Authorize(Policy = Permissions.UserManage)]
    public async Task<IActionResult> SalvarPerfil(string role, string[] permissions, CancellationToken ct)
    {
        if (role == Roles.SuperAdmin || !Roles.All.Contains(role) || permissions.Any(x => !Permissions.All.Contains(x))) return BadRequest();
        await admin.SetRolePermissionsAsync(role, permissions, Actor, ct);
        TempData["Notice"] = "Permissões salvas. As sessões dos usuários deste perfil foram invalidadas.";
        return RedirectToAction(nameof(Perfis));
    }
    [HttpGet("usuarios"), Authorize(Policy = Permissions.UserManage)]
    public async Task<IActionResult> Usuarios(CancellationToken ct) => View(await admin.UsersAsync(ct));
    [HttpPost("usuarios"), Authorize(Policy = Permissions.UserManage)]
    public async Task<IActionResult> CriarUsuario(CreateUserInput model, CancellationToken ct)
    {
        if (!ModelState.IsValid || !Roles.All.Contains(model.Role)) { TempData["Notice"] = "Confira os campos do novo usuário."; return RedirectToAction(nameof(Usuarios)); }
        var user = new ApplicationUser { UserName = model.Email.Trim(), Email = model.Email.Trim(), DisplayName = HtmlText.Clean(model.DisplayName, 180), EmailConfirmed = true, Enabled = true, LockoutEnabled = true };
        var result = await users.CreateAsync(user, model.Password);
        if (result.Succeeded) result = await users.AddToRoleAsync(user, model.Role);
        TempData["Notice"] = result.Succeeded ? "Usuário criado. O primeiro acesso exige configurar o autenticador." : "Não foi possível criar o usuário. Confira e-mail e requisitos de senha.";
        if (result.Succeeded) await Audit("user.created", "user", user.Id, ct);
        return RedirectToAction(nameof(Usuarios));
    }
    [HttpPost("usuarios/{id:long}/estado"), Authorize(Policy = Permissions.UserManage)]
    public async Task<IActionResult> EstadoUsuario(long id, bool enabled, CancellationToken ct)
    {
        if (id == Actor) return BadRequest("Você não pode suspender seu próprio acesso.");
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (await users.IsInRoleAsync(user, Roles.SuperAdmin)) return BadRequest("Administradores principais só podem ser alterados por procedimento operacional.");
        user.Enabled = enabled;
        if (!(await users.UpdateAsync(user)).Succeeded) return Conflict();
        await users.UpdateSecurityStampAsync(user);
        await Audit("user.state_changed", "user", id, ct);
        return RedirectToAction(nameof(Usuarios));
    }
    [HttpPost("usuarios/{id:long}/papel"), Authorize(Policy = Permissions.UserManage)]
    public async Task<IActionResult> Papel(long id, string role, CancellationToken ct)
    {
        if (id == Actor || !Roles.All.Contains(role)) return BadRequest();
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (await users.IsInRoleAsync(user, Roles.SuperAdmin)) return BadRequest();
        var current = await users.GetRolesAsync(user);
        var result = await users.RemoveFromRolesAsync(user, current);
        if (!result.Succeeded || !(await users.AddToRoleAsync(user, role)).Succeeded) return Conflict();
        await users.UpdateSecurityStampAsync(user);
        await Audit("user.role_changed", "user", id, ct);
        return RedirectToAction(nameof(Usuarios));
    }
    [HttpGet("fontes"), Authorize(Policy = Permissions.SourceManage)]
    public async Task<IActionResult> Fontes(CancellationToken ct)
    {
        ViewData["Runs"] = await admin.RunsAsync(ct);
        var sources = await ingestion.SourcesAsync(false, ct);
        var profiles = new Dictionary<long, SourceProfile>();
        foreach (var source in sources) profiles[source.Id] = await discovery.ProfileAsync(source.Id, ct);
        ViewData["Profiles"] = profiles;
        return View(sources);
    }
    [HttpPost("fontes"), Authorize(Policy = Permissions.SourceManage)]
    public async Task<IActionResult> SalvarFonte(SourceInput model, CancellationToken ct)
    {
        if (!ModelState.IsValid || !SafeUrl.Validate(model.FeedUrl).IsValid || !Enum.TryParse<FeedProtocol>(model.Protocol, out var protocol) || !Enum.IsDefined(protocol) ||
            Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(model.FeedUrl).Query).Keys.Any(x => x.ToLowerInvariant() is "key" or "token" or "access_token" or "api_key" or "password"))
        { TempData["Notice"] = "Confira os campos, a URL HTTPS pública e a autorização de uso. Credenciais devem ficar nos segredos do servidor."; return RedirectToAction(nameof(Fontes)); }
        var id = await ingestion.UpsertSourceAsync(new IngestionSource { Name = HtmlText.Clean(model.Name, 200), FeedUrl = model.FeedUrl.Trim(), Protocol = Enum.Parse<FeedProtocol>(model.Protocol), Topics = model.Topics, Municipality = model.Municipality, LicenseNote = model.LicenseNote, Enabled = model.Enabled, Approved = model.Approved, RespectRobots = true }, ct);
        await Audit("source.saved", "source", id, ct);
        await discovery.SaveProfileAsync(new SourceProfile { SourceId = id, OwnerName = model.OwnerName, Organization = model.Organization, Region = model.Region, Keywords = model.Keywords, FrequencyMinutes = model.FrequencyMinutes, DailyRequestLimit = model.DailyRequestLimit, MaxItems = model.MaxItems, CredentialReference = model.CredentialReference }, ct);
        return RedirectToAction(nameof(Fontes));
    }
    [HttpPost("fontes/executar"), Authorize(Policy = Permissions.SourceManage)]
    public async Task<IActionResult> ExecutarColeta(CancellationToken ct)
    {
        await Audit("collector.manual_requested", "collector", null, ct);
        await collector.CollectOnceAsync(ct);
        return RedirectToAction(nameof(Fontes));
    }
    [HttpPost("fontes/{id:long}/suspender"), Authorize(Policy = Permissions.SourceManage)]
    public async Task<IActionResult> SuspenderFonte(long id, CancellationToken ct)
    {
        var source = await ingestion.SourceAsync(id, ct);
        if (source is null) return NotFound();
        source.Enabled = false; await ingestion.UpsertSourceAsync(source, ct); await Audit("source.suspended", "source", id, ct);
        return RedirectToAction(nameof(Fontes));
    }
    [HttpPost("fontes/{id:long}/midia"), Authorize(Policy = Permissions.SourceManage)]
    public async Task<IActionResult> PermitirMidia(long id, bool allowed, string license, CancellationToken ct)
    {
        if (await ingestion.SourceAsync(id, ct) is null || string.IsNullOrWhiteSpace(license) || license.Length > 1000) return BadRequest();
        await admin.SetMediaPermissionAsync(id, allowed, license, Actor, ct);
        TempData["Notice"] = "Permissão de mídia registrada. OCR ainda depende de licença por imagem e configuração no servidor.";
        return RedirectToAction(nameof(Fontes));
    }
    [HttpGet("integracoes"), Authorize(Policy = Permissions.SettingsManage)]
    public async Task<IActionResult> Integracoes(CancellationToken ct)
    {
        ViewData["AiRuns"] = await aiRuns.RecentAsync(30, ct);
        return View(new[] {
            new IntegrationStatus("Google Places", google.IsConfigured, google.IsConfigured ? "Configurada; verificar cotas e autorização no provedor" : "Chave ausente"),
            new IntegrationStatus("Groq", aiOptions.Value.Enabled && !string.IsNullOrWhiteSpace(aiOptions.Value.Groq.ApiKey) && !string.IsNullOrWhiteSpace(aiOptions.Value.Groq.Model), "A execução real aparece no histórico abaixo"),
            new IntegrationStatus("Gemini", aiOptions.Value.Enabled && !string.IsNullOrWhiteSpace(aiOptions.Value.Gemini.ApiKey) && !string.IsNullOrWhiteSpace(aiOptions.Value.Gemini.Model), "A execução real aparece no histórico abaixo"),
            new IntegrationStatus("DeepSeek", aiOptions.Value.Enabled && !string.IsNullOrWhiteSpace(aiOptions.Value.DeepSeek.ApiKey) && !string.IsNullOrWhiteSpace(aiOptions.Value.DeepSeek.Model), "A execução real aparece no histórico abaixo") });
    }
    [HttpGet("auditoria"), Authorize(Policy = Permissions.AuditRead)] public async Task<IActionResult> Auditoria(CancellationToken ct) => View(await audit.RecentAsync(100, ct));
    [HttpGet("configuracoes"), Authorize(Policy = Permissions.SettingsManage)] public async Task<IActionResult> Configuracoes(CancellationToken ct) => View(await admin.SettingsAsync(ct));
    [HttpPost("configuracoes"), Authorize(Policy = Permissions.SettingsManage)]
    public async Task<IActionResult> SalvarConfiguracoes(bool collectorEnabled, int intervalMinutes, CancellationToken ct)
    {
        if (intervalMinutes is < 10 or > 240) return BadRequest();
        await admin.SetSettingAsync("collector.enabled", collectorEnabled ? "true" : "false", Actor, ct);
        await admin.SetSettingAsync("collector.intervalMinutes", intervalMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture), Actor, ct);
        TempData["Notice"] = "Configuração salva. O coletor a consulta a cada ciclo.";
        return RedirectToAction(nameof(Configuracoes));
    }
    [HttpGet("colaboracoes"), Authorize(Policy = Permissions.SubmissionModerate)] public async Task<IActionResult> Colaboracoes(CancellationToken ct) => View(await community.SearchAsync(null, 100, ct));
    [HttpPost("colaboracoes/{id:long}"), Authorize(Policy = Permissions.SubmissionModerate)]
    public async Task<IActionResult> Moderar(long id, bool approve, CancellationToken ct)
    {
        await community.SetStatusAsync(id, approve ? SubmissionStatus.Approved : SubmissionStatus.Rejected, Actor, "Moderação humana", ct);
        await Audit("submission.moderated", "submission", id, ct); return RedirectToAction(nameof(Colaboracoes));
    }
    [HttpGet("lugares"), Authorize(Policy = Permissions.PlaceManage)] public async Task<IActionResult> Lugares(CancellationToken ct) => View(await places.SearchAsync(new PlaceQuery { PublishedOnly = false }, ct));
    [HttpPost("lugares"), Authorize(Policy = Permissions.PlaceManage)]
    public async Task<IActionResult> SalvarLugar(PlaceInput model, CancellationToken ct)
    {
        if (!ModelState.IsValid || model.Category is not null && !MinhaBelem.Infrastructure.Places.PlacesCatalog.IsAllowedType(model.Category) || model.Audiences.Any(x => !Enum.IsDefined(x)) || model.Accessibility.Any(x => !Enum.IsDefined(x))) return BadRequest();
        // Apenas identificador Google e informacoes editoriais proprias. Sem persistir resposta Places.
        var id = await places.UpsertAsync(new Place { GooglePlaceId = model.GooglePlaceId.Trim(), Name = HtmlText.Clean(model.Name, 300), EditorialNote = HtmlText.Clean(model.Note, 1000), Neighborhood = HtmlText.Clean(model.Bairro, 120), Municipality = HtmlText.Clean(model.Municipio, 120), PrimaryType = model.Category, Audiences = model.Audiences, Accessibility = model.Accessibility, Slug = "local-" + Hashing.UrlHashHex(model.GooglePlaceId)[..24], IsPublished = true, CachedAt = DateTime.UtcNow, PurgeAfter = DateTime.UtcNow.AddYears(10) }, ct);
        await Audit("place.curated", "place", id, ct); return RedirectToAction(nameof(Lugares));
    }

    [HttpGet("idiomas"), Authorize(Policy = Permissions.TranslationRead)]
    public async Task<IActionResult> Idiomas(CancellationToken ct)
    {
        ViewData["Stats"] = await translation.GetStatsAsync(ct);
        ViewData["AiRuns"] = (await aiRuns.RecentAsync(50, ct)).Where(r => r.Task == AiTask.Translate).ToList();
        return View(await translation.GetLanguagesAsync(false, ct));
    }

    [HttpPost("idiomas/{code}/estado"), Authorize(Policy = Permissions.TranslationManage)]
    public async Task<IActionResult> SalvarIdiomaEstado(string code, bool enabled, bool requiresApproval, CancellationToken ct)
    {
        var lang = await translation.GetLanguageAsync(code, ct);
        if (lang is null) return NotFound();
        if (lang.IsDefault && !enabled) return BadRequest("O idioma padrão não pode ser desativado.");
        lang.IsEnabled = enabled;
        lang.RequiresHumanApproval = requiresApproval;
        await translation.UpdateLanguageAsync(lang, ct);
        await Audit("language.updated", "language", null, ct);
        TempData["Notice"] = $"Configurações do idioma '{lang.Name}' atualizadas.";
        return RedirectToAction(nameof(Idiomas));
    }

    [HttpGet("traducoes"), Authorize(Policy = Permissions.TranslationRead)]
    public async Task<IActionResult> Traducoes(string? lang, CancellationToken ct)
    {
        var targetLang = string.IsNullOrWhiteSpace(lang) ? "en" : lang;
        ViewData["SelectedLanguage"] = targetLang;
        ViewData["Languages"] = await translation.GetLanguagesAsync(true, ct);
        ViewData["Stats"] = await translation.GetStatsAsync(ct);
        ViewData["PendingContent"] = await translation.PendingContentTranslationsAsync(50, ct);
        ViewData["PendingPlaces"] = await translation.PendingPlaceTranslationsAsync(50, ct);
        ViewData["UntranslatedContent"] = await translation.UntranslatedContentAsync(targetLang, 20, ct);
        ViewData["UntranslatedPlaces"] = await translation.UntranslatedPlacesAsync(targetLang, 20, ct);
        return View();
    }

    [HttpPost("traducoes/conteudo/{id:long}/revisar"), Authorize(Policy = Permissions.TranslationManage)]
    public async Task<IActionResult> RevisarTraducaoConteudo(long id, bool approve, string? notes, CancellationToken ct)
    {
        var status = approve ? TranslationReviewStatus.Approved : TranslationReviewStatus.Rejected;
        await translation.SetContentTranslationStatusAsync(id, status, Actor, notes, ct);
        await Audit("translation.content.reviewed", "translation", id, ct);
        TempData["Notice"] = approve ? "Tradução de conteúdo aprovada para publicação pública." : "Tradução rejeitada.";
        return RedirectToAction(nameof(Traducoes));
    }

    [HttpPost("traducoes/lugar/{id:long}/revisar"), Authorize(Policy = Permissions.TranslationManage)]
    public async Task<IActionResult> RevisarTraducaoLugar(long id, bool approve, string? notes, CancellationToken ct)
    {
        var status = approve ? TranslationReviewStatus.Approved : TranslationReviewStatus.Rejected;
        await translation.SetPlaceTranslationStatusAsync(id, status, Actor, notes, ct);
        await Audit("translation.place.reviewed", "translation", id, ct);
        TempData["Notice"] = approve ? "Tradução de lugar turístico aprovada para publicação pública." : "Tradução rejeitada.";
        return RedirectToAction(nameof(Traducoes));
    }

    [HttpPost("traducoes/solicitar-ia"), Authorize(Policy = Permissions.TranslationManage)]
    public async Task<IActionResult> SolicitarTraducaoAi(string type, long id, string targetLanguage, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(targetLanguage)) return BadRequest();

        if (string.Equals(type, "content", StringComparison.OrdinalIgnoreCase))
        {
            var item = await content.GetAsync(id, ct);
            if (item is null) return NotFound();
            var res = await translationService.TranslateContentAsync(item, targetLanguage, Actor, ct);
            if (!res.Success) TempData["Notice"] = $"Aviso ao traduzir: {res.Error}";
            else TempData["Notice"] = $"Tradução via IA ({res.Provider}) gerada com sucesso! Revise e aprove para publicação.";
            await Audit("translation.ai_requested.content", "content", id, ct);
        }
        else if (string.Equals(type, "place", StringComparison.OrdinalIgnoreCase))
        {
            var place = await places.GetAsync(id, ct);
            if (place is null) return NotFound();
            var res = await translationService.TranslatePlaceAsync(place, targetLanguage, Actor, ct);
            if (!res.Success) TempData["Notice"] = $"Aviso ao traduzir: {res.Error}";
            else TempData["Notice"] = $"Tradução via IA ({res.Provider}) gerada com sucesso! Revise e aprove para publicação.";
            await Audit("translation.ai_requested.place", "place", id, ct);
        }
        return RedirectToAction(nameof(Traducoes), new { lang = targetLanguage });
    }

    [HttpGet("traducoes/comparar/{id:long}"), Authorize(Policy = Permissions.TranslationRead)]
    public async Task<IActionResult> Comparar(long id, string targetLanguage, CancellationToken ct)
    {
        var item = await content.GetAsync(id, ct);
        if (item is null) return NotFound();
        var trans = await translation.GetContentTranslationAsync(id, targetLanguage, ct);
        ViewData["Content"] = item;
        ViewData["Translation"] = trans;
        ViewData["TargetLanguage"] = targetLanguage;
        return View();
    }

    [HttpGet("conteudo"), Authorize(Policy = Permissions.ContentRead)]
    public async Task<IActionResult> Conteudo(string? status, string? categoria, string? busca, CancellationToken ct)
    {
        ContentStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ContentStatus>(status, true, out var parsed))
            parsedStatus = parsed;

        ContentCategory? parsedCategory = null;
        if (!string.IsNullOrWhiteSpace(categoria) && Enum.TryParse<ContentCategory>(categoria, true, out var cat))
            parsedCategory = cat;

        var items = await content.SearchAsync(new ContentQuery
        {
            Status = parsedStatus,
            Category = parsedCategory,
            Search = busca,
            PageSize = 50
        }, ct);

        ViewData["CurrentStatus"] = status;
        ViewData["CurrentCategoria"] = categoria;
        ViewData["CurrentBusca"] = busca;
        return View(items);
    }

    [HttpPost("conteudo"), Authorize(Policy = Permissions.ContentWrite)]
    public async Task<IActionResult> CriarConteudo(string titulo, string resumo, string categoria, string? bairro, string? corpo, string? linkFonte, string? nomeFonte, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(resumo))
        {
            TempData["Notice"] = "Título e resumo são obrigatórios.";
            return RedirectToAction(nameof(Conteudo));
        }

        if (!Enum.TryParse<ContentCategory>(categoria, true, out var cat))
            cat = ContentCategory.Cultura;

        var item = new ContentItem
        {
            Title = HtmlText.Clean(titulo, 250),
            Summary = HtmlText.Clean(resumo, 500),
            Body = !string.IsNullOrWhiteSpace(corpo) ? HtmlText.Clean(corpo, 10000) : null,
            Category = cat,
            Neighborhood = !string.IsNullOrWhiteSpace(bairro) ? HtmlText.Clean(bairro, 120) : "Belém",
            Municipality = "Belém",
            SourceName = !string.IsNullOrWhiteSpace(nomeFonte) ? HtmlText.Clean(nomeFonte, 150) : "Redação MinhaBelém",
            SourceUrl = !string.IsNullOrWhiteSpace(linkFonte) ? linkFonte.Trim() : "https://minhabelem.com.br",
            AuthorName = User.Identity?.Name ?? "Editor MinhaBelém",
            Status = ContentStatus.Published,
            VerificationStatus = VerificationStatus.Verified,
            PublishedAt = DateTime.UtcNow,
            Slug = "noticia-" + Guid.NewGuid().ToString("N")[..12]
        };

        var id = await content.InsertAsync(item, Actor, "Criação editorial manual", ct);
        await Audit("content.created_manually", "content", id, ct);
        TempData["Notice"] = $"Matéria '{item.Title}' publicada com sucesso!";
        return RedirectToAction(nameof(Conteudo));
    }

    [HttpPost("conteudo/{id:long}/arquivar"), Authorize(Policy = Permissions.ContentPublish)]
    public async Task<IActionResult> ArquivarConteudo(long id, CancellationToken ct)
    {
        await content.SetStatusAsync(id, ContentStatus.Archived, Actor, ct);
        await Audit("content.archived", "content", id, ct);
        TempData["Notice"] = $"Conteúdo #{id} foi arquivado.";
        return RedirectToAction(nameof(Conteudo));
    }

    [HttpGet("mobilidade"), Authorize(Policy = Permissions.SettingsManage)]
    public async Task<IActionResult> Mobilidade(CancellationToken ct)
    {
        ViewData["Lines"] = await transit.GetLinesAsync(null, ct);
        ViewData["Alerts"] = await transit.GetActiveAlertsAsync(ct);
        ViewData["RiverRoutes"] = await transit.GetRiverRoutesAsync(ct);
        return View();
    }

    [HttpPost("mobilidade/alerta"), Authorize(Policy = Permissions.SettingsManage)]
    public async Task<IActionResult> CriarAlerta(string titulo, string descricao, string? bairro, string gravidade, string tipo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(descricao))
        {
            TempData["Notice"] = "Título e descrição do alerta são obrigatórios.";
            return RedirectToAction(nameof(Mobilidade));
        }

        var alert = new TransitAlert
        {
            Title = HtmlText.Clean(titulo, 250),
            Description = HtmlText.Clean(descricao, 2000),
            Neighborhood = !string.IsNullOrWhiteSpace(bairro) ? HtmlText.Clean(bairro, 120) : null,
            Severity = gravidade,
            AlertType = tipo,
            StartAt = DateTime.UtcNow,
            IsActive = true,
            CreatedByUserId = Actor
        };

        await transit.UpsertAlertAsync(alert, ct);
        await Audit("transit.alert_created", "transit_alert", null, ct);
        TempData["Notice"] = "Alerta de trânsito cadastrado e ativo!";
        return RedirectToAction(nameof(Mobilidade));
    }

    [HttpPost("mobilidade/linha"), Authorize(Policy = Permissions.SettingsManage)]
    public async Task<IActionResult> SalvarLinha(TransitLine line, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(line.Code) || string.IsNullOrWhiteSpace(line.Name))
        {
            TempData["Notice"] = "Código e nome da linha são obrigatórios.";
            return RedirectToAction(nameof(Mobilidade));
        }

        line.Code = line.Code.Trim().ToUpperInvariant();
        line.Name = HtmlText.Clean(line.Name, 200);
        line.Origin = HtmlText.Clean(line.Origin, 150);
        line.Destination = HtmlText.Clean(line.Destination, 150);

        await transit.UpsertLineAsync(line, ct);
        await Audit("transit.line_saved", "transit_line", null, ct);
        TempData["Notice"] = $"Linha {line.Code} salva com sucesso!";
        return RedirectToAction(nameof(Mobilidade));
    }

    [HttpGet("anunciantes"), Authorize(Policy = Permissions.AdvertiserManage)]
    public async Task<IActionResult> Anunciantes(CancellationToken ct)
    {
        var all = await advertisers.AllAsync(ct);
        return View(all);
    }

    [HttpPost("anunciantes/{id:long}/estado"), Authorize(Policy = Permissions.AdvertiserManage)]
    public async Task<IActionResult> EstadoAnunciante(long id, bool active, CancellationToken ct)
    {
        await advertisers.SetActiveAsync(id, active, ct);
        await Audit("advertiser.state_changed", "campaign", id, ct);
        TempData["Notice"] = active ? "Campanha ativada para veiculação." : "Campanha pausada.";
        return RedirectToAction(nameof(Anunciantes));
    }

    [HttpGet("relatorios"), Authorize(Policy = Permissions.DashboardRead)]
    public async Task<IActionResult> Relatorios(CancellationToken ct)
    {
        ViewData["Dashboard"] = await dashboard.BuildAsync(ct);
        ViewData["TranslationStats"] = await translation.GetStatsAsync(ct);
        ViewData["RecentAudit"] = await audit.RecentAsync(30, ct);
        ViewData["AiCost"] = await aiRuns.CostSinceAsync(DateTime.UtcNow.AddMonths(-1), ct);
        var stats = await aiRuns.StatsSinceAsync(DateTime.UtcNow.AddMonths(-1), ct);
        ViewData["AiStats"] = stats;
        return View();
    }

    [HttpGet("bairros"), Authorize(Policy = Permissions.PlaceManage)]
    public async Task<IActionResult> Bairros(CancellationToken ct)
    {
        return View(await neighborhoods.GetNeighborhoodsAsync(ct));
    }

    [HttpPost("bairros"), Authorize(Policy = Permissions.PlaceManage)]
    public async Task<IActionResult> SalvarBairro(NeighborhoodGuide guide, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(guide.Name) || string.IsNullOrWhiteSpace(guide.Slug))
        {
            TempData["Notice"] = "Nome e slug do bairro são obrigatórios.";
            return RedirectToAction(nameof(Bairros));
        }

        guide.Name = HtmlText.Clean(guide.Name, 120);
        guide.Slug = HtmlText.Clean(guide.Slug.ToLowerInvariant(), 120);
        guide.Zone = HtmlText.Clean(guide.Zone, 50);
        guide.HistorySummary = HtmlText.Clean(guide.HistorySummary, 3000);
        guide.CulturalCharacteristics = HtmlText.Clean(guide.CulturalCharacteristics, 3000);

        await neighborhoods.UpsertNeighborhoodAsync(guide, ct);
        await Audit("neighborhood.saved", "neighborhood", null, ct);
        TempData["Notice"] = $"Guia do bairro '{guide.Name}' salvo com sucesso!";
        return RedirectToAction(nameof(Bairros));
    }
}

