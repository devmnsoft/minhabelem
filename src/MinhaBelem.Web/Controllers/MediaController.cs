using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinhaBelem.Core.Security;
using MySqlConnector;
namespace MinhaBelem.Web.Controllers;

[Route("admin/midias"),Authorize(Policy=Permissions.ContentWrite)]
public sealed class MediaController(MySqlDataSource data,IWebHostEnvironment environment):Controller
{
    private string Storage=>Path.Combine(environment.ContentRootPath,"App_Data","media");
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        await using var db=await data.OpenConnectionAsync(ct);
        return View((await db.QueryAsync<MediaAsset>(new CommandDefinition("SELECT id,description,credit,license,content_id AS ContentId,youtube_id AS YoutubeId FROM mb_media_assets ORDER BY id DESC LIMIT 100",cancellationToken:ct))).AsList());
    }
    [HttpPost(""),RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Upload(IFormFile? image,long? contentId,long? placeId,string description,string credit,string license,string? youtubeId,bool reproductionAuthorized,CancellationToken ct)
    {
        if(!reproductionAuthorized||string.IsNullOrWhiteSpace(description)||description.Length>500||string.IsNullOrWhiteSpace(credit)||credit.Length>300||string.IsNullOrWhiteSpace(license)||license.Length>500||image is null && youtubeId is null)return BadRequest("Informe descrição, crédito, licença e autorização de reprodução.");
        if(youtubeId is not null&&!System.Text.RegularExpressions.Regex.IsMatch(youtubeId,"^[A-Za-z0-9_-]{11}$"))return BadRequest("Identificador YouTube inválido.");
        string? path=null;string? mime=null;string? key=null;
        if(image is not null)
        {
            if(image.Length is <=0 or >4_500_000)return BadRequest("Imagem excede o limite.");
            await using var input=image.OpenReadStream();var header=new byte[12];var n=await input.ReadAsync(header,ct);
            mime=n>=8&&header.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})?"image/png":n>=3&&header[0]==255&&header[1]==216&&header[2]==255?"image/jpeg":n==12&&System.Text.Encoding.ASCII.GetString(header,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(header,8,4)=="WEBP"?"image/webp":null;
            if(mime is null)return BadRequest("Use PNG, JPEG ou WebP.");
            Directory.CreateDirectory(Storage);key=Guid.NewGuid().ToString("N");path=Path.Combine(Storage,key);
            await using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);await output.WriteAsync(header.AsMemory(0,n),ct);await input.CopyToAsync(output,ct);
        }
        try
        {
            await using var db=await data.OpenConnectionAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);
            if(contentId is not null&&await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_content_items WHERE id=@contentId",new{contentId},tx,cancellationToken:ct))!=1)throw new ArgumentException("Conteúdo inexistente.");
            if(placeId is not null&&await db.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM mb_places WHERE id=@placeId",new{placeId},tx,cancellationToken:ct))!=1)throw new ArgumentException("Lugar inexistente.");
            var actor=long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var id=await db.ExecuteScalarAsync<long>(new CommandDefinition("INSERT INTO mb_media_assets(storage_key,mime_type,description,credit,license,content_id,youtube_id,authorized_by) VALUES(@key,@mime,@description,@credit,@license,@contentId,@youtubeId,@actor); SELECT LAST_INSERT_ID()",new{key,mime,description,credit,license,contentId,youtubeId,actor},tx,cancellationToken:ct));
            if(placeId is not null)await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_place_media_assets(media_id,place_id) VALUES(@id,@placeId)",new{id,placeId},tx,cancellationToken:ct));
            await db.ExecuteAsync(new CommandDefinition("INSERT INTO mb_audit_logs(actor_id,action,entity_type,entity_id,details) VALUES(@actor,'media.authorized_upload','media',@id,JSON_OBJECT('license',@license,'contentId',@contentId))",new{actor,id,license,contentId},tx,cancellationToken:ct));
            await tx.CommitAsync(ct);return RedirectToAction(nameof(Index));
        }
        catch(ArgumentException){if(path is not null)System.IO.File.Delete(path);return BadRequest("Conteúdo inexistente.");}
        catch{if(path is not null)System.IO.File.Delete(path);throw;}
    }
    [AllowAnonymous,HttpGet("/midia/{id:long}")]
    public async Task<IActionResult> File(long id,CancellationToken ct)
    {
        await using var db=await data.OpenConnectionAsync(ct);
        var asset=await db.QuerySingleOrDefaultAsync<MediaAsset>(new CommandDefinition("SELECT m.storage_key AS StorageKey,m.mime_type AS MimeType FROM mb_media_assets m LEFT JOIN mb_content_items c ON c.id=m.content_id WHERE m.id=@id AND ((c.status IN ('Published','Cancelled') AND c.verification_status<>'Outdated' AND (c.expires_at IS NULL OR c.expires_at>UTC_TIMESTAMP())) OR EXISTS(SELECT 1 FROM mb_place_media_assets pm JOIN mb_places p ON p.id=pm.place_id WHERE pm.media_id=m.id AND p.is_published=1 AND p.purge_after>UTC_TIMESTAMP()))",new{id},cancellationToken:ct));
        if(asset?.StorageKey is null||!System.Text.RegularExpressions.Regex.IsMatch(asset.StorageKey,"^[a-f0-9]{32}$"))return NotFound();
        var path=Path.Combine(Storage,asset.StorageKey);if(!System.IO.File.Exists(path))return NotFound();
        Response.Headers["X-Content-Type-Options"]="nosniff";Response.Headers.CacheControl="public,max-age=300";return PhysicalFile(path,asset.MimeType!);
    }
}
public sealed class MediaAsset{public long Id{get;set;}public long? ContentId{get;set;}public string? StorageKey{get;set;}public string? MimeType{get;set;}public string? YoutubeId{get;set;}public string Description{get;set;}="";public string Credit{get;set;}="";public string License{get;set;}="";}
