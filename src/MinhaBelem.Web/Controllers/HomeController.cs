using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Web.Services;
namespace MinhaBelem.Web.Controllers;
public sealed class HomeController(ContentRepository repository):Controller {
 public async Task<IActionResult> Index(string? categoria,string? busca,CancellationToken ct) {
  if(categoria is {Length:>60}||busca is {Length:>120})return BadRequest();
  ViewData["Category"]=categoria;ViewData["Search"]=busca;return View(await repository.RecentAsync(categoria,busca,ct));
 }
 public async Task<IActionResult> Detalhe(long id,CancellationToken ct) {var item=await repository.FindAsync(id,ct);return item is null?NotFound():View(item);}
 [HttpPost][ValidateAntiForgeryToken] public async Task<IActionResult> Colaborar(string nome,string mensagem,CancellationToken ct){
  if(string.IsNullOrWhiteSpace(nome)||nome.Length>100||string.IsNullOrWhiteSpace(mensagem)||mensagem.Length>2500){TempData["Notice"]="Confira os campos.";return RedirectToAction(nameof(Index));}
  await repository.SubmitAsync(nome.Trim(),mensagem.Trim(),ct);TempData["Notice"]="Sugestão enviada para revisão.";return RedirectToAction(nameof(Index));
 }
 public IActionResult Error()=>View();
}