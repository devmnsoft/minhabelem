namespace MinhaBelem.Core.Taxonomy;

/// <summary>Público-alvo de eventos e servicios do guia.</summary>
public enum Audience
{
    Infantil,
    Juvenil,
    Adulto,
    MelhorIdade
}

/// <summary>
/// Recursos de acessibilidade. A API Google Places não os fornece: são curadoria editorial
/// registrada com fonte verificável, por isso ficam separados do dado do provedor.
/// </summary>
public enum AccessibilityFeature
{
    Braille,
    Auditiva,
    Intelectual,
    Fisica,
    BanheiroAdaptado,
    Elevador,
    EstacionamentoAdaptado,
    RotaAcessivel
}