using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Web.Services;
namespace MinhaBelem.Web.Controllers;
[Route("editorial")]
public sealed class EditorialController(EditorialRepository editorial,IConfiguration config):Controller {
 [HttpGet("")][Authorize(AuthenticationSchemes=CookieAuthenticationDefaults.AuthenticationScheme)]
 public async Task<IActionResult> Index(CancellationToken ct)=>View(await editorial.PendingAsync(ct));
 [HttpGet("login")][AllowAnonymous]public IActionResult Login()=>View();
 [HttpPost("login")][AllowAnonymous][ValidateAntiForgeryToken]
 public async Task<IActionResult> LoginPost([FromForm]string token) {
  var expected=config["Editorial:AccessToken"];
  if(string.IsNullOrWhiteSpace(expected)||expected.Length<32||string.IsNullOrWhiteSpace(token)||!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),SHA256.HashData(Encoding.UTF8.GetBytes(token))))return Unauthorized("Acesso não autorizado.");
  var identity=new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"temporary-editor"),new Claim(ClaimTypes.Role,"Editor")},CookieAuthenticationDefaults.AuthenticationScheme);
  await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(identity),new AuthenticationProperties{IsPersistent=false,ExpiresUtc=DateTimeOffset.UtcNow.AddHours(2)});
  return RedirectToAction(nameof(Index));
 }
 [HttpPost("logout")][Authorize][ValidateAntiForgeryToken]
 public async Task<IActionResult> Logout(){await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);return RedirectToAction(nameof(Login));}
 [HttpPost("review/{id:long}")][Authorize(Roles="Editor")][ValidateAntiForgeryToken]
 public async Task<IActionResult> Review(long id,[FromForm]bool approve,CancellationToken ct){
  if(id<=0)return BadRequest();
  var ok=await editorial.ReviewAsync(id,approve,ct);
  return ok?RedirectToAction(nameof(Index)):NotFound();
 }
}