namespace MinhaBelem.Core.Entities;

public enum TranslationReviewStatus
{
    Pending,
    Approved,
    Rejected
}

public sealed class Language
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
    public string FlagEmoji { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }
    public bool RequiresHumanApproval { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ContentTranslation
{
    public long Id { get; set; }
    public long ContentId { get; set; }
    public string SourceLanguage { get; set; } = "pt-BR";
    public string TargetLanguage { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? CulturalNotes { get; set; }
    public string Provider { get; set; } = "Manual";
    public string Model { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public TranslationReviewStatus ReviewStatus { get; set; } = TranslationReviewStatus.Pending;
    public long? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class PlaceTranslation
{
    public long Id { get; set; }
    public long PlaceId { get; set; }
    public string SourceLanguage { get; set; } = "pt-BR";
    public string TargetLanguage { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? EditorialNote { get; set; }
    public string? CulturalNotes { get; set; }
    public string Provider { get; set; } = "Manual";
    public string Model { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public TranslationReviewStatus ReviewStatus { get; set; } = TranslationReviewStatus.Pending;
    public long? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CulturalGlossaryItem
{
    public long Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ExplanationPt { get; set; } = string.Empty;
    public string ExplanationEn { get; set; } = string.Empty;
    public string ExplanationFr { get; set; } = string.Empty;
    public string ExplanationEs { get; set; } = string.Empty;
    public string ExplanationRu { get; set; } = string.Empty;
    public string ExplanationZh { get; set; } = string.Empty;
    public string ExplanationJa { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public string GetExplanation(string? culture) => (culture?.ToLowerInvariant()) switch
    {
        "en" => !string.IsNullOrWhiteSpace(ExplanationEn) ? ExplanationEn : ExplanationPt,
        "fr" => !string.IsNullOrWhiteSpace(ExplanationFr) ? ExplanationFr : ExplanationPt,
        "es" => !string.IsNullOrWhiteSpace(ExplanationEs) ? ExplanationEs : ExplanationPt,
        "ru" => !string.IsNullOrWhiteSpace(ExplanationRu) ? ExplanationRu : ExplanationPt,
        "zh" or "zh-hans" or "zh-cn" => !string.IsNullOrWhiteSpace(ExplanationZh) ? ExplanationZh : ExplanationPt,
        "ja" => !string.IsNullOrWhiteSpace(ExplanationJa) ? ExplanationJa : ExplanationPt,
        _ => ExplanationPt
    };
}

public sealed class TouristItinerary
{
    public long Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public int Days { get; set; } = 1;
    public string Theme { get; set; } = string.Empty;
    public string TitlePt { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string TitleFr { get; set; } = string.Empty;
    public string TitleEs { get; set; } = string.Empty;
    public string TitleRu { get; set; } = string.Empty;
    public string TitleZh { get; set; } = string.Empty;
    public string TitleJa { get; set; } = string.Empty;
    public string SummaryPt { get; set; } = string.Empty;
    public string SummaryEn { get; set; } = string.Empty;
    public string SummaryFr { get; set; } = string.Empty;
    public string SummaryEs { get; set; } = string.Empty;
    public string SummaryRu { get; set; } = string.Empty;
    public string SummaryZh { get; set; } = string.Empty;
    public string SummaryJa { get; set; } = string.Empty;
    public string DescriptionPt { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
    public string DescriptionFr { get; set; } = string.Empty;
    public string DescriptionEs { get; set; } = string.Empty;
    public string DescriptionRu { get; set; } = string.Empty;
    public string DescriptionZh { get; set; } = string.Empty;
    public string DescriptionJa { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<TouristItineraryStop> Stops { get; set; } = Array.Empty<TouristItineraryStop>();

    public string GetTitle(string? culture) => (culture?.ToLowerInvariant()) switch
    {
        "en" => !string.IsNullOrWhiteSpace(TitleEn) ? TitleEn : TitlePt,
        "fr" => !string.IsNullOrWhiteSpace(TitleFr) ? TitleFr : TitlePt,
        "es" => !string.IsNullOrWhiteSpace(TitleEs) ? TitleEs : TitlePt,
        "ru" => !string.IsNullOrWhiteSpace(TitleRu) ? TitleRu : TitlePt,
        "zh" or "zh-hans" or "zh-cn" => !string.IsNullOrWhiteSpace(TitleZh) ? TitleZh : TitlePt,
        "ja" => !string.IsNullOrWhiteSpace(TitleJa) ? TitleJa : TitlePt,
        _ => TitlePt
    };

    public string GetSummary(string? culture) => (culture?.ToLowerInvariant()) switch
    {
        "en" => !string.IsNullOrWhiteSpace(SummaryEn) ? SummaryEn : SummaryPt,
        "fr" => !string.IsNullOrWhiteSpace(SummaryFr) ? SummaryFr : SummaryPt,
        "es" => !string.IsNullOrWhiteSpace(SummaryEs) ? SummaryEs : SummaryPt,
        "ru" => !string.IsNullOrWhiteSpace(SummaryRu) ? SummaryRu : SummaryPt,
        "zh" or "zh-hans" or "zh-cn" => !string.IsNullOrWhiteSpace(SummaryZh) ? SummaryZh : SummaryPt,
        "ja" => !string.IsNullOrWhiteSpace(SummaryJa) ? SummaryJa : SummaryPt,
        _ => SummaryPt
    };

    public string GetDescription(string? culture) => (culture?.ToLowerInvariant()) switch
    {
        "en" => !string.IsNullOrWhiteSpace(DescriptionEn) ? DescriptionEn : DescriptionPt,
        "fr" => !string.IsNullOrWhiteSpace(DescriptionFr) ? DescriptionFr : DescriptionPt,
        "es" => !string.IsNullOrWhiteSpace(DescriptionEs) ? DescriptionEs : DescriptionPt,
        "ru" => !string.IsNullOrWhiteSpace(DescriptionRu) ? DescriptionRu : DescriptionPt,
        "zh" or "zh-hans" or "zh-cn" => !string.IsNullOrWhiteSpace(DescriptionZh) ? DescriptionZh : DescriptionPt,
        "ja" => !string.IsNullOrWhiteSpace(DescriptionJa) ? DescriptionJa : DescriptionPt,
        _ => DescriptionPt
    };
}

public sealed class TouristItineraryStop
{
    public long Id { get; set; }
    public long ItineraryId { get; set; }
    public int DayNumber { get; set; } = 1;
    public int StopOrder { get; set; } = 1;
    public long? PlaceId { get; set; }
    public string TitlePt { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string PeriodOfDay { get; set; } = "Manhã";
    public string TipPt { get; set; } = string.Empty;
    public string TipEn { get; set; } = string.Empty;
    public string TipFr { get; set; } = string.Empty;
    public string TipEs { get; set; } = string.Empty;
    public string TipRu { get; set; } = string.Empty;
    public string TipZh { get; set; } = string.Empty;
    public string TipJa { get; set; } = string.Empty;
    public string EstimatedDuration { get; set; } = "2 horas";
    public Place? Place { get; set; }

    public string GetTitle(string? culture) => (culture?.ToLowerInvariant()) switch
    {
        "en" => !string.IsNullOrWhiteSpace(TitleEn) ? TitleEn : TitlePt,
        _ => TitlePt
    };

    public string GetTip(string? culture) => (culture?.ToLowerInvariant()) switch
    {
        "en" => !string.IsNullOrWhiteSpace(TipEn) ? TipEn : TipPt,
        "fr" => !string.IsNullOrWhiteSpace(TipFr) ? TipFr : TipPt,
        "es" => !string.IsNullOrWhiteSpace(TipEs) ? TipEs : TipPt,
        "ru" => !string.IsNullOrWhiteSpace(TipRu) ? TipRu : TipPt,
        "zh" or "zh-hans" or "zh-cn" => !string.IsNullOrWhiteSpace(TipZh) ? TipZh : TipPt,
        "ja" => !string.IsNullOrWhiteSpace(TipJa) ? TipJa : TipPt,
        _ => TipPt
    };
}
