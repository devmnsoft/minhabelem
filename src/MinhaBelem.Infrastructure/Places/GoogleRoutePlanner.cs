using System.Net.Http.Json;
using System.Text.Json;
using System.Globalization;
using Dapper;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Policies;
namespace MinhaBelem.Infrastructure.Places;
/// <summary>Only aggregate usage is persisted. Visitor origins and Google route content are never stored.</summary>
public sealed class GoogleRoutePlanner(IHttpClientFactory clients,IConfiguration config,MySqlDataSource data):IRoutePlanner
{
    public async Task<RoutePlan> PlanAsync(RouteQuery q,CancellationToken ct)
    {
        if(q.Origin.Length>350||q.Destination.Length is <2 or >350||q.Mode is not("transit" or "walking" or "bicycling" or "driving" or "boat")||q.Preference is not("less_walking" or "less_transfers" or "fastest" or "cheapest")||q.MaxWalkMeters is <0 or >10000)throw new ArgumentException("Confira origem, destino e preferências.");
        var external="https://www.google.com/maps/dir/?api=1&destination="+Uri.EscapeDataString(q.Destination)+"&travelmode="+(q.Mode is "walking" or "bicycling" or "driving"?q.Mode:"transit")+(string.IsNullOrWhiteSpace(q.Origin)?"":"&origin="+Uri.EscapeDataString(q.Origin));
        var queried=false;
        RoutePlan Result(string state,string message,IReadOnlyList<RouteOption>? routes=null)=>new(state,"Google Routes",message,queried?DateTime.UtcNow:null,routes??[],external);
        if(q.Mode=="boat")return new RoutePlan("unavailable","SEGBEL","Travessia fluvial sem operação digital confirmada. Consulte operador e fonte oficial para embarque, maré e retorno.",null,[],"https://seguranca.belem.pa.gov.br/");
        if(q.Accessible)return Result("unavailable","Este provedor não confirma acessibilidade integral da viagem. Confirme os trechos e os veículos com a operadora.");
        if(q.ArriveBy&&q.Mode=="driving")return Result("unavailable","Este provedor não calcula carro por horário de chegada. Informe o horário de saída ou consulte o serviço oficial.");
        var key=config["GoogleRoutes:ApiKey"];
        if(!config.GetValue<bool>("GoogleRoutes:Enabled")||string.IsNullOrWhiteSpace(key)||config.GetValue<int>("GoogleRoutes:DailyLimit")<=0)return Result("unconfigured","Consulta interna de rotas indisponível. Use o planejador oficial do Google Maps.");
        if(string.IsNullOrWhiteSpace(q.Origin))return Result("unavailable","Informe um endereço, bairro ou referência de origem.");
        if(!await ReserveAsync(ct))return Result("budget","Limite diário atingido. Continue no Google Maps.");
        var mode=q.Mode switch{"walking"=>"WALK","bicycling"=>"BICYCLE","driving"=>"DRIVE",_=>"TRANSIT"};
        var language=q.Culture switch{"en" or "fr" or "es" or "ru" or "ja"=>q.Culture,"zh-Hans"=>"zh-CN",_=>"pt-BR"};
        var body=new Dictionary<string,object>{["origin"]=new{address=q.Origin},["destination"]=new{address=q.Destination},["travelMode"]=mode,["computeAlternativeRoutes"]=true,["languageCode"]=language,["regionCode"]="BR"};
        if(q.LocalTime is {} wall){var utc=TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(wall,DateTimeKind.Unspecified),VisitRecommendationService.Zone);if(utc<DateTime.UtcNow.AddMinutes(-5)||utc>DateTime.UtcNow.AddDays(365))throw new ArgumentException("Data fora do intervalo permitido.");if(mode=="TRANSIT")body[q.ArriveBy?"arrivalTime":"departureTime"]=utc.ToString("O");else if(mode=="DRIVE"&&!q.ArriveBy)body["departureTime"]=utc.ToString("O");}
        if(mode=="TRANSIT"&&q.Preference is "less_transfers" or "less_walking")body["transitPreferences"]=new{routingPreference=q.Preference=="less_transfers"?"FEWER_TRANSFERS":"LESS_WALKING"};
        using var request=new HttpRequestMessage(HttpMethod.Post,"https://routes.googleapis.com/directions/v2:computeRoutes"){Content=JsonContent.Create(body)};
        request.Headers.Add("X-Goog-Api-Key",key);request.Headers.Add("X-Goog-FieldMask","routes.description,routes.distanceMeters,routes.duration,routes.polyline.encodedPolyline,routes.travelAdvisory.transitFare,routes.legs.steps.travelMode,routes.legs.steps.distanceMeters,routes.legs.steps.navigationInstruction.instructions,routes.legs.steps.transitDetails");
        try
        {
            queried=true;
            using var response=await clients.CreateClient("routes").SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
            if(!response.IsSuccessStatusCode){await FailureAsync("HTTP "+(int)response.StatusCode,ct);return Result("error","O provedor não retornou a rota. Continue no Google Maps.");}
            if(response.Content.Headers.ContentLength>2_000_000)throw new JsonException();
            var bytes=await response.Content.ReadAsByteArrayAsync(ct);if(bytes.Length>2_000_000)throw new JsonException();
            using var json=JsonDocument.Parse(bytes);var options=Parse(json.RootElement,q.MaxWalkMeters);
            if(q.Preference=="cheapest")options=options.OrderBy(x=>x.Fare.HasValue&&x.Currency=="BRL"?0:1).ThenBy(x=>x.Currency=="BRL"?x.Fare:null).ToArray();
            else if(q.Preference=="fastest")options=options.OrderBy(x=>x.DurationSeconds).ToArray();
            return Result(options.Count>0?"available":"unavailable",options.Count>0?"Estimativas fornecidas pelo Google. Confira mudanças e tarifas na fonte; menor custo só compara tarifas disponíveis.":"Nenhuma rota compatível informada pelo provedor.",options);
        }
        catch(Exception ex) when(ex is HttpRequestException or JsonException || ex is OperationCanceledException&&!ct.IsCancellationRequested){await FailureAsync(ex.GetType().Name,ct);return Result("error","Consulta indisponível. Continue no planejador oficial.");}
    }
    public static IReadOnlyList<RouteOption> Parse(JsonElement root,int maxWalk)
    {
        static JsonElement Child(JsonElement e,string p)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(p,out var v)?v:default;
        static string? Text(JsonElement e,string p)=>Child(e,p).ValueKind==JsonValueKind.String?Child(e,p).GetString():null;
        static int? Number(JsonElement e,string p)=>Child(e,p).TryGetInt32Safe();
        static DateTimeOffset? Time(JsonElement e,string p)=>DateTimeOffset.TryParse(Text(e,p),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var v)?v:null;
        var result=new List<RouteOption>();var routes=Child(root,"routes");if(routes.ValueKind!=JsonValueKind.Array)return result;
        foreach(var r in routes.EnumerateArray().Take(3))
        {
            if(Number(r,"distanceMeters") is not {} distance||distance<0||!double.TryParse(Text(r,"duration")?.TrimEnd('s'),CultureInfo.InvariantCulture,out var seconds)||seconds<=0||seconds>int.MaxValue)continue;
            var legs=Child(r,"legs");var steps=new List<RouteStep>();var walk=0;var unknownWalk=false;
            if(legs.ValueKind==JsonValueKind.Array)foreach(var leg in legs.EnumerateArray()){var segments=Child(leg,"steps");if(segments.ValueKind!=JsonValueKind.Array)continue;foreach(var s in segments.EnumerateArray()){var mode=Text(s,"travelMode")??"";var meters=Number(s,"distanceMeters");if(mode=="WALK"){walk+=meters??0;unknownWalk|=meters is null;}var transit=Child(s,"transitDetails");var stops=Child(transit,"stopDetails");var line=Child(transit,"transitLine");steps.Add(new(mode,Text(Child(s,"navigationInstruction"),"instructions")??Text(transit,"headsign")??"Trecho informado pelo provedor",meters,Text(line,"nameShort")??Text(line,"name"),Text(Child(stops,"departureStop"),"name"),Text(Child(stops,"arrivalStop"),"name"),Time(stops,"departureTime"),Time(stops,"arrivalTime")));}}
            if(steps.Count==0||walk>maxWalk||unknownWalk)continue;
            var money=Child(Child(r,"travelAdvisory"),"transitFare");decimal? fare=null;var currency=Text(money,"currencyCode");if(currency is not null&&decimal.TryParse(Text(money,"units")??"0",NumberStyles.Integer,CultureInfo.InvariantCulture,out var units))fare=units+(Number(money,"nanos")??0)/1_000_000_000m;
            result.Add(new(Text(r,"description")??"Trajeto Google Maps",distance,(int)seconds,steps,fare,currency,Text(Child(r,"polyline"),"encodedPolyline")));
        }
        return result;
    }
    private async Task<bool> ReserveAsync(CancellationToken ct){await using var db=await data.OpenConnectionAsync(ct);await db.ExecuteAsync(new CommandDefinition("INSERT IGNORE INTO mb_route_api_usage(day) VALUES(UTC_DATE())",cancellationToken:ct));return await db.ExecuteAsync(new CommandDefinition("UPDATE mb_route_api_usage SET requests=requests+1 WHERE day=UTC_DATE() AND requests<@limit",new{limit=Math.Clamp(config.GetValue<int>("GoogleRoutes:DailyLimit"),0,10000)},cancellationToken:ct))==1;}
    private async Task FailureAsync(string error,CancellationToken ct){await using var db=await data.OpenConnectionAsync(ct);await db.ExecuteAsync(new CommandDefinition("UPDATE mb_route_api_usage SET failures=failures+1,last_error=@error WHERE day=UTC_DATE()",new{error},cancellationToken:ct));}
}
internal static class JsonRouteExtensions{internal static int? TryGetInt32Safe(this JsonElement e)=>e.ValueKind==JsonValueKind.Number&&e.TryGetInt32(out var v)?v:null;}
