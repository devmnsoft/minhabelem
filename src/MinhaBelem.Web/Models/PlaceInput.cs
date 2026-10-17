using System.ComponentModel.DataAnnotations;
using MinhaBelem.Core.Taxonomy;
namespace MinhaBelem.Web.Models;
public sealed class PlaceInput
{
    [Required, StringLength(255), RegularExpression("^[A-Za-z0-9_-]+$")] public string GooglePlaceId { get; set; } = "";
    [Required, StringLength(300)] public string Name { get; set; } = "";
    [StringLength(1000)] public string? Note { get; set; }
    [StringLength(120)] public string? Bairro { get; set; }
    [StringLength(120)] public string? Municipio { get; set; }
    public string? Category { get; set; }
    public Audience[] Audiences { get; set; } = Array.Empty<Audience>();
    public AccessibilityFeature[] Accessibility { get; set; } = Array.Empty<AccessibilityFeature>();
}
