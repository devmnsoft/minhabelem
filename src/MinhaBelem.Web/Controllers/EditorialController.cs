using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Web.Services;
namespace MinhaBelem.Web.Controllers;
[Route("editorial")]
public sealed class EditorialController(EditorialRepository editorial,IConfiguration config):Controller {
 private bool Authorized() {
  var expected=config["Editorial:AccessToken"];
  if(string.IsNullOrWhiteSpace(expected)||expected.Length<32)return false;
  var current=Request.Cookies["mb_editor"];
  if(string.IsNullOrWhiteSpace(current))return false;
  return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),SHA256.HashData(Encoding.UTF8.GetBytes(current)));
 }
 [HttpGet("")]public async Task<IActionResult> Index(CancellationToken ct) {
  if(!Authorized())return RedirectToAction(nameof(Login));
  return View(await editorial.PendingAsync(ct));
 }
 [HttpGet("login")]public IActionResult Login()=>View();
 [HttpPost("login")][ValidateAntiForgeryToken]
 public IActionResult LoginPost([FromForm]string token) {
  var expected=config["Editorial:AccessToken"];
  if(string.IsNullOrWhiteSpace(expected)||expected.Length<32||string.IsNullOrWhiteSpace(token)||!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),SHA256.HashData(Encoding.UTF8.GetBytes(token))))return Unauthorized("Acesso não autorizado.");
  Response.Cookies.Append("mb_editor",token,new CookieOptions{HttpOnly=true,Secure=true,SameSite=SameSiteMode.Strict,MaxAge=TimeSpan.FromHours(2),Path="/editorial"});
  return RedirectToAction(nameof(Index));
 }
 [HttpPost("logout")][ValidateAntiForgeryToken]public IActionResult Logout(){Response.Cookies.Delete("mb_editor",new CookieOptions{Path="/editorial"});return RedirectToAction(nameof(Login));}
 [HttpPost("review/{id:long}")][ValidateAntiForgeryToken]
 public async Task<IActionResult> Review(long id,[FromForm]bool approve,CancellationToken ct){
  if(!Authorized())return Unauthorized();
  if(id<=0)return BadRequest();
  var ok=await editorial.ReviewAsync(id,approve,ct);
  return ok?RedirectToAction(nameof(Index)):NotFound();
 }
}