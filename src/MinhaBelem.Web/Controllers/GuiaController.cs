using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Web.Services;
namespace MinhaBelem.Web.Controllers;
[Route("guia")]
public sealed class GuiaController(PlacesService places):Controller {
 [HttpGet("")]public async Task<IActionResult> Index([FromQuery]string categoria="tourist_attraction",CancellationToken ct=default){
  var allowed=new[]{"tourist_attraction","restaurant","museum","movie_theater","park","hospital","library","cafe","art_gallery","shopping_mall","lodging","stadium"};
  if(!allowed.Contains(categoria,StringComparer.OrdinalIgnoreCase))return BadRequest();
  ViewData["Category"]=categoria;return View(await places.SearchAsync(categoria,ct));
 }
}