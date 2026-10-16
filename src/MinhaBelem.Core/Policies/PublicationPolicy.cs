using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Taxonomy;
using TaxonomyInfo = MinhaBelem.Core.Taxonomy.Taxonomy;

namespace MinhaBelem.Core.Policies;

public sealed record PolicyDecision(bool Allowed, bool RequiresHumanApproval, IReadOnlyList<string> Reasons)
{
    public static PolicyDecision Blocked(params string[] reasons) => new(false, true, reasons);
    public static PolicyDecision Automated(params string[] reasons) => new(true, false, reasons);
    public static PolicyDecision HumanOnly(params string[] reasons) => new(false, true, reasons);
}

/// <summary>
/// Regra editorial central: define o que pode sair automaticamente e o que exige pessoa.
/// A IA nunca publica sozinha política, denúncia ou alerta crítico.
/// </summary>
public static class PublicationPolicy
{
    public static PolicyDecision Evaluate(ContentItem item, DateTime now, bool automated)
    {
        var reasons = new List<string>();

        if (string.IsNullOrWhiteSpace(item.Title)) reasons.Add("Título obrigatório.");
        if (string.IsNullOrWhiteSpace(item.Summary)) reasons.Add("Resumo obrigatório.");
        if (string.IsNullOrWhiteSpace(item.SourceUrl)) reasons.Add("URL da fonte obrigatória.");
        if (item.Summary.Length > 2500) reasons.Add("Resumo excede 2500 caracteres.");
        if (item.Title.Length > 300) reasons.Add("Título excede 300 caracteres.");
        if (automated && item.RequiresHumanApproval) reasons.Add("Item aguarda aprovação humana.");

        if (item.Category == ContentCategory.Politica)
            reasons.Add("Notícia política exige aprovação editorial humana.");
        if (string.Equals(item.Subject, TaxonomyInfo.SubjectByKey("denuncia")?.Key, StringComparison.OrdinalIgnoreCase))
            reasons.Add("Denúncia exige aprovação editorial humana.");
        if (item.Flags.HasFlag(ModerationFlag.Denunciation))
            reasons.Add("Marcada como denúncia pelo screening de segurança.");
        if (item.Flags.HasFlag(ModerationFlag.CriticalAlert))
            reasons.Add("Alerta crítico exige aprovação editorial humana.");
        if (item.Flags.HasFlag(ModerationFlag.Offensive))
            reasons.Add("Conteúdo potencialmente ofensivo retido até moderação.");
        if (item.Flags.HasFlag(ModerationFlag.Sensitive))
            reasons.Add("Conteúdo sensível retido até revisão humana.");
        if (item.Flags.HasFlag(ModerationFlag.Outdated))
            reasons.Add("Conteúdo sinalizado como desatualizado.");

        if (item.VerificationStatus == VerificationStatus.Outdated)
            reasons.Add("Verificação marcada como desatualizada.");

        if (item.Kind == ContentKind.Event && item.EventStartAt.HasValue && (item.EventEndAt ?? item.EventStartAt.Value) <= now)
            reasons.Add("Evento já realizado não pode ser publicado.");

        if (item.DuplicateOfId is > 0)
            reasons.Add("Item marcado como duplicidade.");

        if (reasons.Count == 0)
            return new PolicyDecision(true, !automated && !item.RequiresHumanApproval, Array.Empty<string>());

        var allowedForHuman = !reasons.Any(r => r.StartsWith("Título obrigatório", StringComparison.Ordinal)
                                             || r.StartsWith("Resumo obrigatório", StringComparison.Ordinal)
                                             || r.StartsWith("URL da fonte obrigatória", StringComparison.Ordinal)
                                             || r.StartsWith("Resumo excede", StringComparison.Ordinal)
                                             || r.StartsWith("Título excede", StringComparison.Ordinal)
                                             || r.StartsWith("Evento já realizado", StringComparison.Ordinal));

        return new PolicyDecision(allowedForHuman && !automated, true, reasons);
    }

    /// <summary>Motivo pelo qual a automação é impedida de publicar. Usado na central editorial.</summary>
    public static IReadOnlyList<string> AutomationBlockers(ContentItem item) =>
        Evaluate(item, DateTime.UtcNow, automated: true).Reasons;
}

/// <summary>Regras de expiração e varredura de conteúdo antigo.</summary>
public static class FreshnessPolicy
{
    public const int EventStaleDays = 1;
    public const int NewsStaleDays = 120;
    public const int ServiceStaleDays = 365;

    public static bool IsOutdated(ContentItem item, DateTime now)
    {
        var reference = item.EventStartAt ?? item.PublishedAt;
        if (reference == default) return false;
        var days = item.Kind switch
        {
            ContentKind.Event => EventStaleDays,
            ContentKind.Service => ServiceStaleDays,
            _ => NewsStaleDays
        };
        return (now - reference).TotalDays > days;
    }

    public static DateTime? SuggestedExpiry(ContentItem item, DateTime now) => item.Kind switch
    {
        ContentKind.Event when item.EventStartAt is { } start => start.AddDays(EventStaleDays),
        _ => null
    };
}
