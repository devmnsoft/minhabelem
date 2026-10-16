using Dapper;
using Npgsql;
namespace MinhaBelem.Web.Services;
public sealed record EditorialCandidate(long Id,string Title,string Summary,string SourceUrl,DateTimeOffset PublishedAt,string SourceName);
public sealed class EditorialRepository(NpgsqlDataSource source) {
 public async Task<IReadOnlyList<EditorialCandidate>> PendingAsync(CancellationToken ct) {
  await using var db=await source.OpenConnectionAsync(ct);
  var query=@"SELECT c.id,c.title,c.summary,c.source_url AS SourceUrl,c.published_at AS PublishedAt,s.source_name AS SourceName FROM ingestion_candidates c JOIN ingestion_sources s ON s.id=c.source_id WHERE c.status='pending' ORDER BY c.created_at DESC LIMIT 100";
  return (await db.QueryAsync<EditorialCandidate>(new CommandDefinition(query,cancellationToken:ct))).AsList();
 }
 public async Task<bool> ReviewAsync(long id,bool approve,CancellationToken ct) {
  await using var db=await source.OpenConnectionAsync(ct);
  await using var transaction=await db.BeginTransactionAsync(ct);
  var candidate=await db.QuerySingleOrDefaultAsync<EditorialCandidate>(new CommandDefinition(@"SELECT c.id,c.title,c.summary,c.source_url AS SourceUrl,c.published_at AS PublishedAt,s.source_name AS SourceName FROM ingestion_candidates c JOIN ingestion_sources s ON s.id=c.source_id WHERE c.id=@id AND c.status='pending' FOR UPDATE OF c",new{id},transaction,cancellationToken:ct));
  if(candidate is null)return false;
  if(approve) {
   await db.ExecuteAsync(new CommandDefinition(@"INSERT INTO content_items(category,title,summary,source_name,source_url,published_at,last_verified_at,verification_status,status) VALUES ('noticias',@Title,@Summary,@SourceName,@SourceUrl,@PublishedAt,now(),'pending','review') ON CONFLICT (source_url) DO NOTHING",candidate,transaction,cancellationToken:ct));
  }
  await db.ExecuteAsync(new CommandDefinition("UPDATE ingestion_candidates SET status=@status WHERE id=@id",new{id,status=approve?"approved":"rejected"},transaction,cancellationToken:ct));
  await db.ExecuteAsync(new CommandDefinition("INSERT INTO audit_logs(action,entity_type,entity_id,details) VALUES (@action,'ingestion_candidate',@id,'{}'::jsonb)",new{id,action=approve?"candidate_approved_to_review":"candidate_rejected"},transaction,cancellationToken:ct));
  await transaction.CommitAsync(ct);return true;
 }
}