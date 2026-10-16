using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Services;
using MinhaBelem.Core.Text;
using MinhaBelem.Infrastructure.Ai;
using MinhaBelem.Infrastructure.Background;
using MinhaBelem.Infrastructure.Identity;
using MinhaBelem.Web.Models;
namespace MinhaBelem.Web.Controllers;

[Route("admin"), Authorize(Policy = Permissions.DashboardRead)]
public sealed class AdminController(IAdministrationRepository admin, IDashboardRepository dashboard, IIngestionRepository ingestion, IAuditRepository audit,
    IAiRunRepository aiRuns, UserManager<ApplicationUser> users, ICommunityRepository community, IPlaceRepository places, IPlacesClient google, IOptions<AiOptions> aiOptions,
    IDiscoveryRepository discovery, ISourceCollection collector) : Controller
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private Task Audit(string action, string type, long? id, CancellationToken ct) => audit.WriteAsync(new AuditEntry { ActorId = Actor, Action = action, EntityType = type, EntityId = id }, ct);
    [HttpGet("")] public async Task<IActionResult> Index(CancellationToken ct) => View(await dashboard.BuildAsync(ct));
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
    [HttpGet("integracoes"), Authorize(Policy = Permissions.SettingsManage)]
    public async Task<IActionResult> Integracoes(CancellationToken ct)
    {
        ViewData["AiRuns"] = await aiRuns.RecentAsync(30, ct);
        return View(new[] {
            new IntegrationStatus("Google Places", google.IsConfigured, google.IsConfigured ? "Configurada; verificar cotas e autorização no provedor" : "Chave ausente"),
            new IntegrationStatus("Groq", !string.IsNullOrWhiteSpace(aiOptions.Value.Groq.ApiKey), "A execução real aparece no histórico abaixo"),
            new IntegrationStatus("Gemini", !string.IsNullOrWhiteSpace(aiOptions.Value.Gemini.ApiKey), "A execução real aparece no histórico abaixo"),
            new IntegrationStatus("DeepSeek", !string.IsNullOrWhiteSpace(aiOptions.Value.DeepSeek.ApiKey), "A execução real aparece no histórico abaixo") });
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
    public async Task<IActionResult> SalvarLugar(string googlePlaceId, string name, string note, string? bairro, string? municipio, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(googlePlaceId) || googlePlaceId.Length > 255 || string.IsNullOrWhiteSpace(name) || name.Length > 300 || note?.Length > 1000 || bairro?.Length > 120 || municipio?.Length > 120) return BadRequest();
        // Apenas identificador Google e informacoes editoriais proprias. Sem persistir resposta Places.
        var id = await places.UpsertAsync(new Place { GooglePlaceId = googlePlaceId.Trim(), Name = HtmlText.Clean(name, 300), EditorialNote = HtmlText.Clean(note, 1000), Neighborhood = HtmlText.Clean(bairro, 120), Municipality = HtmlText.Clean(municipio, 120), Slug = "local-" + Hashing.UrlHashHex(googlePlaceId)[..24], IsPublished = true, CachedAt = DateTime.UtcNow, PurgeAfter = DateTime.UtcNow.AddYears(10) }, ct);
        await Audit("place.curated", "place", id, ct); return RedirectToAction(nameof(Lugares));
    }
}
