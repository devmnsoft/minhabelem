using Dapper;
using MySqlConnector;
using MinhaBelem.Web.Models;
namespace MinhaBelem.Web.Services;
public sealed class ContentRepository(MySqlDataSource db) {
 const string Columns="id,category,title,summary,neighborhood,source_name AS SourceName,source_url AS SourceUrl,image_url AS ImageUrl,author_name AS AuthorName,event_at AS EventAt,published_at AS PublishedAt,last_verified_at AS LastVerifiedAt,verification_status AS VerificationStatus,ticket_price AS TicketPrice,address";
 public async Task<IReadOnlyList<ContentItem>> RecentAsync(string? category,string? search,CancellationToken ct){
  await using var c=await db.OpenConnectionAsync(ct);
  var sql=$"SELECT {Columns} FROM content_items WHERE status='published' AND (@category IS NULL OR category=@category) AND (@search IS NULL OR title LIKE CONCAT('%',@search,'%') OR summary LIKE CONCAT('%',@search,'%')) ORDER BY published_at DESC LIMIT 80";
  return (await c.QueryAsync<ContentItem>(new CommandDefinition(sql,new{category,search},cancellationToken:ct))).AsList();
 }
 public async Task<ContentItem?> FindAsync(long id,CancellationToken ct){
  await using var c=await db.OpenConnectionAsync(ct);
  return await c.QuerySingleOrDefaultAsync<ContentItem>(new CommandDefinition($"SELECT {Columns} FROM content_items WHERE id=@id AND status='published'",new{id},cancellationToken:ct));
 }
 public async Task SubmitAsync(string name,string message,CancellationToken ct){
  await using var c=await db.OpenConnectionAsync(ct);
  await c.ExecuteAsync(new CommandDefinition("INSERT INTO community_submissions(display_name,message,status) VALUES (@name,@message,'pending')",new{name,message},cancellationToken:ct));
 }
}