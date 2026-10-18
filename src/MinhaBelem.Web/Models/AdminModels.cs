using System.ComponentModel.DataAnnotations;
namespace MinhaBelem.Web.Models;
public sealed class CreateUserInput
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, StringLength(180)] public string DisplayName { get; set; } = "";
    [Required, StringLength(256, MinimumLength = 12)] public string Password { get; set; } = "";
    [Required] public string Role { get; set; } = "Editor";
}
public sealed class SourceInput
{
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [Required, StringLength(2000)] public string FeedUrl { get; set; } = "";
    public string Protocol { get; set; } = "Rss";
    [StringLength(255)] public string? Topics { get; set; }
    [StringLength(120)] public string? Municipality { get; set; }
    [Required, StringLength(500)] public string LicenseNote { get; set; } = "";
    public bool Enabled { get; set; }
    public bool Approved { get; set; }
    [StringLength(180)] public string OwnerName { get; set; } = "";
    [StringLength(200)] public string Organization { get; set; } = "";
    [StringLength(120)] public string Region { get; set; } = "Pará";
    [StringLength(500)] public string Keywords { get; set; } = "";
    [Range(1, 1440)] public int FrequencyMinutes { get; set; } = 10;
    [Range(1, 1000)] public int DailyRequestLimit { get; set; } = 24;
    [Range(1, 100)] public int MaxItems { get; set; } = 20;
    [RegularExpression("^[a-zA-Z0-9_-]{1,80}$")] public string? CredentialReference { get; set; }
}
public sealed record IntegrationStatus(string Name, bool Configured, string Status);
