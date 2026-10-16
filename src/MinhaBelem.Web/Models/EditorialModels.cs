using System.ComponentModel.DataAnnotations;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Taxonomy;
namespace MinhaBelem.Web.Models;
public sealed record EditorialIndex(IReadOnlyList<IngestionCandidate> Candidates, IReadOnlyList<Core.Entities.ContentItem> Content);
public sealed class ContentInput
{
    public long Id { get; set; }
    public int Revision { get; set; }
    [Required, StringLength(300)] public string Title { get; set; } = "";
    [Required, StringLength(2500)] public string Summary { get; set; } = "";
    [StringLength(20000)] public string? Body { get; set; }
    [Required, StringLength(240)] public string SourceName { get; set; } = "";
    [Required, StringLength(2000)] public string SourceUrl { get; set; } = "";
    [Required, StringLength(240)] public string AuthorName { get; set; } = "";
    [EnumDataType(typeof(ContentCategory))] public ContentCategory Category { get; set; } = ContentCategory.Cidades;
    [EnumDataType(typeof(ContentKind))] public ContentKind Kind { get; set; }
    [StringLength(120)] public string? Municipality { get; set; }
    [StringLength(120)] public string? Neighborhood { get; set; }
    [StringLength(500)] public string? Address { get; set; }
    public DateTime? EventStartAt { get; set; }
    public DateTime? EventEndAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    [Range(0, 999999999)] public decimal? TicketPrice { get; set; }
    public bool IsFree { get; set; }
    public bool Verified { get; set; }
    [Required, StringLength(480)] public string ChangeSummary { get; set; } = "Revisão editorial";
}
