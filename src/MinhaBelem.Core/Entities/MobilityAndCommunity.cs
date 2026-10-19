using System.Globalization;

namespace MinhaBelem.Core.Entities;

public sealed class TransitLine
{
    public string? SourceUrl { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool IsVerified => MinhaBelem.Core.Security.SafeUrl.Validate(SourceUrl).IsValid && VerifiedAt <= DateTime.UtcNow && ValidUntil > DateTime.UtcNow;
    public DateTime? FareValidUntil { get; set; }
    public decimal? ConfirmedFare => IsVerified && FareValidUntil > DateTime.UtcNow ? Fare : null;

    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public decimal Fare { get; set; }
    public string TransitType { get; set; } = "Urbano"; // Urbano, BRT, Distrital, Fluvial
    public string? OperatorName { get; set; }
    public bool HasAirConditioning { get; set; }
    public bool HasWheelchairLift { get; set; }
    public string? ItinerarySummary { get; set; }
    /// <summary>Dias de operação publicados pela fonte oficial (ex.: "Todos os dias").</summary>
    public string? ServiceDays { get; set; }
    /// <summary>Primeira saída do dia, conforme tabela de serviços.</summary>
    public TimeSpan? FirstDeparture { get; set; }
    /// <summary>Última saída do dia, conforme tabela de serviços.</summary>
    public TimeSpan? LastDeparture { get; set; }
    /// <summary>Intervalo médio entre veículos, em minutos.</summary>
    public int? FrequencyMinutes { get; set; }
    /// <summary>Terminais e estações de integração atendidos pela linha.</summary>
    public string? IntegrationPoints { get; set; }
    /// <summary>Observações operacionais publicadas (entre pico, modalidade de integração, etc.).</summary>
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Horário de operação formatado para exibição.</summary>
    public string ScheduleText
    {
        get
        {
            if (!IsVerified) return "Confirme o horário com o operador";
            if (FirstDeparture is null || LastDeparture is null) return "Horário não publicado";
            var inicio = FirstDeparture.Value.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            var fim = LastDeparture.Value.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            var dias = string.IsNullOrWhiteSpace(ServiceDays) ? "" : $" · {ServiceDays}";
            var freq = FrequencyMinutes is > 0 ? $" · intervalo de {FrequencyMinutes} min" : "";
            return $"{inicio} às {fim}{dias}{freq}";
        }
    }
}

/// <summary>Ponto do itinerário oficial de uma linha, na ordem de percurso.</summary>
public sealed class TransitRoutePoint
{
    public long Id { get; set; }
    public string LineCode { get; set; } = string.Empty;
    public string Direction { get; set; } = "IDA"; // IDA | VOLTA
    public int Position { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Kind { get; set; } = "Eixo"; // Eixo | Terminal | Estação | Integração | Referência
    public long? StopId { get; set; }
    public string? StopName { get; set; }
    public bool IsIntegration => Kind is "Terminal" or "Estação" or "Integração";
}

public sealed class TransitStop
{
    public string? SourceUrl { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool IsVerified => MinhaBelem.Core.Security.SafeUrl.Validate(SourceUrl).IsValid && VerifiedAt <= DateTime.UtcNow && ValidUntil > DateTime.UtcNow;

    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Neighborhood { get; set; } = string.Empty;
    public string Municipality { get; set; } = "Belém";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string StopType { get; set; } = "Parada"; // Parada, Terminal, Estação BRT, Trapiche Fluvial
    public bool IsAccessible { get; set; }
    public string? LinesServiced { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class TransitAlert
{
    public string? SourceUrl { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool IsVerified => MinhaBelem.Core.Security.SafeUrl.Validate(SourceUrl).IsValid && VerifiedAt <= DateTime.UtcNow && ValidUntil > DateTime.UtcNow;

    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Neighborhood { get; set; }
    public string Severity { get; set; } = "Media"; // Baixa, Media, Alta, Critica
    public string AlertType { get; set; } = "Obras"; // Obras, Interdicao, Desvio, Clima, Evento
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public bool IsActive { get; set; } = true;
    public long? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class RiverRoute
{
    public string? SourceUrl { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public bool IsVerified => MinhaBelem.Core.Security.SafeUrl.Validate(SourceUrl).IsValid && VerifiedAt <= DateTime.UtcNow && ValidUntil > DateTime.UtcNow;
    public DateTime? FareValidUntil { get; set; }
    public decimal? ConfirmedFare => IsVerified && FareValidUntil > DateTime.UtcNow ? Fare : null;

    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeparturePort { get; set; } = string.Empty;
    public string DestinationPort { get; set; } = string.Empty;
    public string DestinationIsland { get; set; } = string.Empty;
    public int EstimatedDurationMinutes { get; set; }
    public decimal Fare { get; set; }
    public string VesselType { get; set; } = "Popopô / Barco Regional";
    public string OperatingHours { get; set; } = "";
    public string? OperatorNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public sealed class NeighborhoodGuide
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty; // Centro Histórico, Região Central, Zona Sul, Zona Norte, Ilhas e Distritos
    public string HistorySummary { get; set; } = string.Empty;
    public string CulturalCharacteristics { get; set; } = string.Empty;
    public string? SafetyTips { get; set; }
    public string? PopularAttractions { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CampaignMetric
{
    public long Id { get; set; }
    public long CampaignId { get; set; }
    public DateOnly MetricDate { get; set; }
    public int Impressions { get; set; }
    public int Clicks { get; set; }
    public string? Category { get; set; }
    public string? Neighborhood { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AdvertiserLead
{
    public long Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string PlanInterest { get; set; } = "Padrao";
    public string? Notes { get; set; }
    public bool IsContacted { get; set; }
    public DateTime CreatedAt { get; set; }
}
