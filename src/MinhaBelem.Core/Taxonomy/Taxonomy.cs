namespace MinhaBelem.Core.Taxonomy;

/// <summary>Categorias editoriais do MinhaBelém. O valor persistido é o nome do membro.</summary>
public enum ContentCategory
{
    Cultura,
    Familia,
    Esporte,
    Politica,
    Mobilidade,
    Servicos,
    Turismo,
    Publico,
    Cidades
}

public sealed record CategoryInfo(ContentCategory Category, string Slug, string Label, string ShortLabel, string Icon);

/// <summary>Catálogo fixo de categorias e assuntos. Fonte única para UI, filtros e classificação.</summary>
public static class Taxonomy
{
    public static readonly IReadOnlyList<CategoryInfo> All = new[]
    {
        new CategoryInfo(ContentCategory.Cultura,    "cultura",    "Cultura",              "Cultura",    "teatro"),
        new CategoryInfo(ContentCategory.Familia,    "familia",    "Família e Religião",   "Família",    "familia"),
        new CategoryInfo(ContentCategory.Esporte,    "esporte",    "Esporte",             "Esporte",    "esporte"),
        new CategoryInfo(ContentCategory.Politica,   "politica",   "Política e Administração", "Cidadania", "politica"),
        new CategoryInfo(ContentCategory.Mobilidade, "mobilidade", "Mobilidade",          "Mobilidade", "onibus"),
        new CategoryInfo(ContentCategory.Servicos,   "servicos",   "Serviços",            "Serviços",   "servicos"),
        new CategoryInfo(ContentCategory.Turismo,    "turismo",    "Turismo e Lazer",      "Turismo",    "turismo"),
        new CategoryInfo(ContentCategory.Publico,    "publico",    "Serviços Públicos",    "Público",    "publico"),
        new CategoryInfo(ContentCategory.Cidades,    "cidades",    "Belém e Pará",         "Cidades",    "cidades")
    };

    private static readonly Dictionary<ContentCategory, CategoryInfo> ByCategory =
        All.ToDictionary(x => x.Category);

    private static readonly Dictionary<string, ContentCategory> BySlug =
        All.ToDictionary(x => x.Slug, x => x.Category, StringComparer.OrdinalIgnoreCase);

    public static CategoryInfo Info(ContentCategory category) => ByCategory[category];

    public static bool TryParseSlug(string? slug, out ContentCategory category)
    {
        category = default;
        return !string.IsNullOrWhiteSpace(slug) && BySlug.TryGetValue(slug.Trim(), out category);
    }

    public static bool TryParseName(string? name, out ContentCategory category)
    {
        category = default;
        return !string.IsNullOrWhiteSpace(name) && Enum.TryParse(name.Trim(), ignoreCase: true, out category) && Enum.IsDefined(category);
    }

    /// <summary>Converte entrada livre (slug ou nome) para categoria. Retorna null quando inválida.</summary>
    public static ContentCategory? Resolve(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var key = value.Trim();
        if (BySlug.TryGetValue(key, out var bySlug)) return bySlug;
        if (Enum.TryParse(key, ignoreCase: true, out ContentCategory byName) && Enum.IsDefined(byName)) return byName;
        return null;
    }

    /// <summary>Categorias cujo conteúdo nunca pode ser publicado sem aprovação humana.</summary>
    public static IReadOnlyCollection<ContentCategory> AlwaysHumanReview => new[]
    {
        ContentCategory.Politica
    };

    /// <summary>Assuntos cobertos pela coleta, conforme escopo do produto.</summary>
    public static IReadOnlyList<SubjectInfo> Subjects { get; } = new[]
    {
        new SubjectInfo("exposicoes",       ContentCategory.Cultura,    "Exposições"),
        new SubjectInfo("teatro",           ContentCategory.Cultura,    "Teatro"),
        new SubjectInfo("cinema",           ContentCategory.Cultura,    "Cinema"),
        new SubjectInfo("eventos-culturais",ContentCategory.Cultura,    "Eventos culturais"),
        new SubjectInfo("infantil",         ContentCategory.Familia,    "Eventos infantis"),
        new SubjectInfo("familia",          ContentCategory.Familia,    "Eventos familiares"),
        new SubjectInfo("religioso",        ContentCategory.Familia,    "Religioso"),
        new SubjectInfo("competicoes",      ContentCategory.Esporte,    "Competições"),
        new SubjectInfo("esportes",         ContentCategory.Esporte,    "Esportes"),
        new SubjectInfo("politica",         ContentCategory.Politica,   "Notícias políticas"),
        new SubjectInfo("administracao",    ContentCategory.Politica,   "Administração pública"),
        new SubjectInfo("denuncia",         ContentCategory.Politica,   "Denúncias"),
        new SubjectInfo("mobilidade",       ContentCategory.Mobilidade, "Mobilidade urbana"),
        new SubjectInfo("interdicoes",      ContentCategory.Mobilidade, "Interdições"),
        new SubjectInfo("transporte",       ContentCategory.Mobilidade, "Transporte"),
        new SubjectInfo("saude",            ContentCategory.Servicos,   "Saúde"),
        new SubjectInfo("educacao",         ContentCategory.Servicos,   "Educação"),
        new SubjectInfo("saneamento",       ContentCategory.Servicos,   "Saneamento"),
        new SubjectInfo("turismo",          ContentCategory.Turismo,    "Turismo"),
        new SubjectInfo("gastronomia",      ContentCategory.Turismo,    "Gastronomia"),
        new SubjectInfo("lazer",            ContentCategory.Turismo,    "Lazer"),
        new SubjectInfo("servicos-publicos",ContentCategory.Publico,    "Serviços públicos"),
        new SubjectInfo("feriados",         ContentCategory.Publico,    "Feriados e datas comemorativas"),
        new SubjectInfo("belem",            ContentCategory.Cidades,    "Belém"),
        new SubjectInfo("interior-do-para", ContentCategory.Cidades,    "Demais municípios do Pará")
    };

    private static readonly Dictionary<string, SubjectInfo> BySubject =
        Subjects.ToDictionary(x => x.Key, x => x, StringComparer.OrdinalIgnoreCase);

    public static SubjectInfo? ResolveSubject(string? key) =>
        !string.IsNullOrWhiteSpace(key) && BySubject.TryGetValue(key.Trim(), out var s) ? s : null;

    public static SubjectInfo? SubjectByKey(string key) => ResolveSubject(key);
}

public sealed record SubjectInfo(string Key, ContentCategory Category, string Label);
