using Dapper;
using MinhaBelem.Core.Abstractions;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.MySql;

public sealed class VisitRequestRepository(MySqlDataSource data) : IVisitRequests
{
    public async Task<long> SubmitAsync(VisitRequest request, CancellationToken ct)
    {
        await using var conn = await data.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition("""
            INSERT INTO mb_visit_requests (place_id, contact_name, email, phone, party_size, preferred_on, message, consented_at)
            VALUES (@PlaceId, @ContactName, @Email, @Phone, @PartySize, @PreferredOn, @Message, UTC_TIMESTAMP(6));
            SELECT LAST_INSERT_ID();
            """, request, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<VisitRequest>> PendingAsync(CancellationToken ct)
    {
        await using var conn = await data.OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<VisitRequest>(new CommandDefinition("""
            SELECT CAST(r.id AS SIGNED) AS Id, CAST(r.place_id AS SIGNED) AS PlaceId, p.name AS PlaceName,
                   r.contact_name AS ContactName, r.email AS Email, r.phone AS Phone, r.party_size AS PartySize,
                   r.preferred_on AS PreferredOn, r.message AS Message, r.handled AS Handled, r.created_at AS CreatedAt
            FROM mb_visit_requests r
            JOIN mb_places p ON p.id=r.place_id
            WHERE r.handled=0
            ORDER BY r.id
            LIMIT 100
            """, cancellationToken: ct));
        return rows.ToArray();
    }

    public async Task<bool> HandleAsync(long id, CancellationToken ct)
    {
        await using var conn = await data.OpenConnectionAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition("UPDATE mb_visit_requests SET handled=1 WHERE id=@id AND handled=0", new { id }, cancellationToken: ct)) == 1;
    }
}
