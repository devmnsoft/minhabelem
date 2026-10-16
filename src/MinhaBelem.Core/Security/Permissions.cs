namespace MinhaBelem.Core.Security;

/// <summary>Nomes de papel. O valor é persistido no banco e usado em <c>[Authorize(Roles=...)]</c>.</summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Editor = "Editor";
    public const string Moderator = "Moderator";
    public const string Advertiser = "Advertiser";

    public static readonly string[] All = [SuperAdmin, Editor, Moderator, Advertiser];
}

/// <summary>
/// Permissões por funcionalidade. Um usuário pode ter vários papéis; a permissão efetiva é a união.
/// A matriz padrão papel → permissão fica em <see cref="RolePermissionDefaults"/>.
/// </summary>
public static class Permissions
{
    public const string DashboardRead = "dashboard.read";
    public const string ContentRead = "content.read";
    public const string ContentWrite = "content.write";
    public const string ContentPublish = "content.publish";
    public const string ContentDelete = "content.delete";
    public const string CandidateRead = "candidate.read";
    public const string CandidateReview = "candidate.review";
    public const string SourceManage = "source.manage";
    public const string PlaceManage = "place.manage";
    public const string SubmissionModerate = "submission.moderate";
    public const string AdvertiserManage = "advertiser.manage";
    public const string AiRun = "ai.run";
    public const string AuditRead = "audit.read";
    public const string UserManage = "user.manage";
    public const string SettingsManage = "settings.manage";
    public const string TranslationRead = "translation.read";
    public const string TranslationManage = "translation.manage";

    public static readonly string[] All =
    [
        DashboardRead, ContentRead, ContentWrite, ContentPublish, ContentDelete,
        CandidateRead, CandidateReview, SourceManage, PlaceManage, SubmissionModerate,
        AdvertiserManage, AiRun, AuditRead, UserManage, SettingsManage,
        TranslationRead, TranslationManage
    ];

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [DashboardRead] = "Ver painel com indicadores reais",
        [ContentRead] = "Ver conteúdo em qualquer estado",
        [ContentWrite] = "Criar e editar conteúdo",
        [ContentPublish] = "Publicar, despublicar e arquivar",
        [ContentDelete] = "Excluir conteúdo definitivamente",
        [CandidateRead] = "Ver candidatos coletados",
        [CandidateReview] = "Aprovar ou rejeitar candidatos",
        [SourceManage] = "Cadastrar e ativar fontes de coleta",
        [PlaceManage] = "Curar lugares do guia",
        [SubmissionModerate] = "Moderar colaborações da comunidade",
        [AdvertiserManage] = "Gerenciar campanhas de anunciantes",
        [AiRun] = "Executar análise de inteligência artificial",
        [AuditRead] = "Consultar trilha de auditoria",
        [UserManage] = "Gerenciar usuários, papéis e permissões",
        [SettingsManage] = "Alterar configurações da plataforma",
        [TranslationRead] = "Ver idiomas, traduções e métricas de internacionalização",
        [TranslationManage] = "Gerenciar, solicitar IA e aprovar traduções de conteúdo"
    };

    public static readonly IReadOnlyDictionary<string, string[]> ByRole = new Dictionary<string, string[]>
    {
        [Roles.SuperAdmin] = All,
        [Roles.Editor] =
        [
            DashboardRead, ContentRead, ContentWrite, ContentPublish,
            CandidateRead, CandidateReview, SourceManage, AiRun, SubmissionModerate, PlaceManage,
            TranslationRead, TranslationManage
        ],
        [Roles.Moderator] =
        [
            DashboardRead, ContentRead, CandidateRead, CandidateReview, SubmissionModerate, AiRun, PlaceManage,
            TranslationRead
        ],
        [Roles.Advertiser] = [AdvertiserManage]
    };
}