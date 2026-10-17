using System.ComponentModel.DataAnnotations;
namespace MinhaBelem.Web.Models;
public sealed class LoginInput
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, StringLength(256)] public string Password { get; set; } = "";
}
public sealed class PasswordInput
{
    [Required] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(256, MinimumLength = 12)] public string NewPassword { get; set; } = "";
}
public sealed record MfaModel(string? Secret, string? Uri, bool Enabled);
