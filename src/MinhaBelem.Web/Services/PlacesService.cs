using System.Net.Http.Json;
using System.Text.Json;
namespace MinhaBelem.Web.Services;
public sealed record PlaceCard(string Id,string Name,string? Address,double? Latitude,double? Longitude,string? MapsUrl,string? PhotoName,string? Category);
public sealed class PlacesService(IHttpClientFactory clients,IConfiguration config,ILogger<PlacesService> logger) {
 private static readonly HashSet<string> AllowedCategories=new(StringComparer.OrdinalIgnoreCase){"restaurant","tourist_attraction","museum","movie_theater","park","hospital","library","cafe","art_gallery","shopping_mall","lodging","stadium"};
 public async Task<IReadOnlyList<PlaceCard>> SearchAsync(string category,CancellationToken ct){
  if(!AllowedCategories.Contains(category))return [];
  var key=config["GooglePlaces:ApiKey"];if(string.IsNullOrWhiteSpace(key))return [];
  var client=clients.CreateClient("places");using var request=new HttpRequestMessage(HttpMethod.Post,"https://places.googleapis.com/v1/places:searchNearby");
  request.Headers.Add("X-Goog-Api-Key",key);
  request.Headers.Add("X-Goog-FieldMask","places.id,places.displayName,places.formattedAddress,places.location,places.googleMapsUri,places.photos.name,places.primaryType");
  request.Content=JsonContent.Create(new {includedTypes=new[]{category},maxResultCount=16,locationRestriction=new{circle=new{center=new{latitude=-1.4558,longitude=-48.4902},radius=14000.0}},languageCode="pt-BR"});
  try{
   using var response=await client.SendAsync(request,ct);if(!response.IsSuccessStatusCode){logger.LogWarning("Google Places respondeu {Status}",response.StatusCode);return [];}
   using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
   if(!doc.RootElement.TryGetProperty("places",out var places))return [];
   var list=new List<PlaceCard>();
   foreach(var p in places.EnumerateArray()){
    string? Get(string name)=>p.TryGetProperty(name,out var v)?v.GetString():null;
    double? lat=null,lon=null;if(p.TryGetProperty("location",out var loc)){if(loc.TryGetProperty("latitude",out var a))lat=a.GetDouble();if(loc.TryGetProperty("longitude",out var b))lon=b.GetDouble();}
    var display=p.TryGetProperty("displayName",out var d)&&d.TryGetProperty("text",out var t)?t.GetString():null;
    if(string.IsNullOrWhiteSpace(display))continue;
    list.Add(new(Get("id")??"",display,Get("formattedAddress"),lat,lon,Get("googleMapsUri"),null,Get("primaryType")));
   }
   return list;
  }catch(Exception ex)when(ex is HttpRequestException or TaskCanceledException or JsonException){logger.LogWarning(ex,"Falha Google Places");return [];}
 }
}