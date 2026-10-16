using System.ServiceModel.Syndication;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Dapper;
using MySqlConnector;
namespace MinhaBelem.Web.Services;
public sealed class FeedCollector(IServiceScopeFactory scopes,IHttpClientFactory factory,IConfiguration cfg,ILogger<FeedCollector> logger):BackgroundService {
 protected override async Task ExecuteAsync(CancellationToken token){
  using var timer=new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(10,cfg.GetValue("Collector:IntervalMinutes",10))));
  if(cfg.GetValue("Collector:Enabled",false))await Collect(token);
  while(await timer.WaitForNextTickAsync(token))if(cfg.GetValue("Collector:Enabled",false))await Collect(token);
 }
 private async Task Collect(CancellationToken ct){
  using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<MySqlDataSource>();
  await using var con=await db.OpenConnectionAsync(ct);
  var client=factory.CreateClient("feed");
  var feeds=await con.QueryAsync<(long Id,string FeedUrl)>(new CommandDefinition("SELECT id,feed_url AS FeedUrl FROM ingestion_sources WHERE enabled=1 AND approved=1 AND protocol='rss' LIMIT 30",cancellationToken:ct));
  foreach(var feed in feeds)try {
   // Somente fontes explicitamente aprovadas. Proteger contra SSRF e redirects antes de habilitar em produção.
   var uri=new Uri(feed.FeedUrl);
   if(uri.Scheme!="https"||uri.IsLoopback||uri.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase))continue;
   using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct);
   response.EnsureSuccessStatusCode();
   if(response.Content.Headers.ContentLength is > 2000000)continue;
   await using var stream=await response.Content.ReadAsStreamAsync(ct);
   using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2_000_000});
   var feedData=SyndicationFeed.Load(reader);
   foreach(var item in feedData.Items.Take(35)){
    var url=item.Links.FirstOrDefault(x=>x.Uri?.Scheme=="https")?.Uri?.AbsoluteUri;
    if(url is null||url.Length>2000)continue;
    var title=item.Title?.Text?.Trim();
    if(string.IsNullOrWhiteSpace(title))continue;
    var summary=System.Text.RegularExpressions.Regex.Replace(item.Summary?.Text??"","<[^>]*>"," ");
    var digest=SHA256.HashData(Encoding.UTF8.GetBytes(url));
    var date=(item.PublishDate==default?DateTimeOffset.UtcNow:item.PublishDate).UtcDateTime;
    await con.ExecuteAsync(new CommandDefinition(@"INSERT INTO ingestion_candidates(source_id,title,summary,source_url,source_url_hash,published_at,status) VALUES (@sid,@title,@summary,@url,@digest,@date,'pending') ON DUPLICATE KEY UPDATE id=id",new{sid=feed.Id,title=title[..Math.Min(300,title.Length)],summary=summary[..Math.Min(1500,summary.Length)],url,digest,date},cancellationToken:ct));
   }
  }catch(Exception ex){logger.LogWarning(ex,"Falha ao ler fonte {FeedId}",feed.Id);}
 }
}