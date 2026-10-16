using Dapper;
namespace MinhaBelem.Infrastructure.Networking;
public sealed class ApiBudget(MySqlDataSource data)
{
    public async Task<bool> ReserveAsync(string service, int limit, CancellationToken ct)
    {
        await using var db = await data.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("INSERT IGNORE INTO mb_api_usage (service,day,requests) VALUES (@service,UTC_DATE(),0)", new { service }, cancellationToken: ct));
        return await db.ExecuteAsync(new CommandDefinition("UPDATE mb_api_usage SET requests=requests+1 WHERE service=@service AND day=UTC_DATE() AND requests<@limit", new { service, limit = Math.Clamp(limit, 1, 10000) }, cancellationToken: ct)) == 1;
    }
}
