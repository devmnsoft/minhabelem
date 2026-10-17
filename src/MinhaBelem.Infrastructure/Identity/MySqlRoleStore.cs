using Dapper;
using Microsoft.AspNetCore.Identity;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.Identity;

public sealed class MySqlRoleStore(MySqlDataSource dataSource)
    : IRoleClaimStore<IdentityRole<long>>
{
    public void Dispose() { }
    public Task AddClaimAsync(IdentityRole<long> role, Claim claim, CancellationToken ct = default) => AddClaimsAsync(role, new[] { claim }, ct);
    public Task RemoveClaimAsync(IdentityRole<long> role, Claim claim, CancellationToken ct = default) => RemoveClaimsAsync(role, new[] { claim }, ct);

    private async Task<MySqlConnection> OpenAsync(CancellationToken ct)
        => (MySqlConnection)await dataSource.OpenConnectionAsync(ct);

    public async Task<IdentityResult> CreateAsync(IdentityRole<long> role, CancellationToken cancellationToken)
    {
        role.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await using var connection = await OpenAsync(cancellationToken);
        var id = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            """
            INSERT INTO mb_roles (name, normalized_name, concurrency_stamp)
            VALUES (@Name, @NormalizedName, @ConcurrencyStamp);
            SELECT LAST_INSERT_ID();
            """,
            new { role.Name, NormalizedName = role.NormalizedName ?? role.Name?.ToUpperInvariant(), role.ConcurrencyStamp },
            cancellationToken: cancellationToken));
        if (id is null or <= 0)
            return IdentityResult.Failed(new IdentityError { Code = "InsertFailed", Description = "Não foi possível criar o papel." });
        role.Id = id.Value;
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(IdentityRole<long> role, CancellationToken cancellationToken)
    {
        role.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE mb_roles SET name = @Name, normalized_name = @NormalizedName, concurrency_stamp = @ConcurrencyStamp WHERE id = @Id",
            new { role.Name, NormalizedName = role.NormalizedName ?? role.Name?.ToUpperInvariant(), role.ConcurrencyStamp, role.Id },
            cancellationToken: cancellationToken));
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(IdentityRole<long> role, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM mb_roles WHERE id = @id", new { role.Id }, cancellationToken: cancellationToken));
        return IdentityResult.Success;
    }

    public Task<IdentityRole<long>?> FindByIdAsync(string roleId, CancellationToken cancellationToken) =>
        FindOneAsync("id", long.Parse(roleId), cancellationToken);

    public Task<IdentityRole<long>?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken) =>
        FindOneAsync("normalized_name", normalizedRoleName, cancellationToken);

    private async Task<IdentityRole<long>?> FindOneAsync(string column, object value, CancellationToken ct)
    {
        if (column is not ("id" or "normalized_name")) throw new ArgumentOutOfRangeException(nameof(column));
        await using var connection = await OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<IdentityRole<long>>(new CommandDefinition(
            $"SELECT id AS Id, name AS Name, normalized_name AS NormalizedName, concurrency_stamp AS ConcurrencyStamp FROM mb_roles WHERE {column} = @value LIMIT 1",
            new { value }, cancellationToken: ct));
    }

    public Task<string> GetRoleIdAsync(IdentityRole<long> role, CancellationToken cancellationToken) =>
        Task.FromResult(role.Id.ToString());

    public Task<string?> GetRoleNameAsync(IdentityRole<long> role, CancellationToken cancellationToken) => Task.FromResult(role.Name);

    public Task SetRoleNameAsync(IdentityRole<long> role, string? roleName, CancellationToken cancellationToken)
    {
        role.Name = roleName;
        role.NormalizedName = roleName?.ToUpperInvariant();
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedRoleNameAsync(IdentityRole<long> role, CancellationToken cancellationToken) => Task.FromResult(role.NormalizedName);

    public Task SetNormalizedRoleNameAsync(IdentityRole<long> role, string? normalizedName, CancellationToken cancellationToken)
    {
        role.NormalizedName = normalizedName;
        return Task.CompletedTask;
    }

    public Task<string?> GetConcurrencyStampAsync(IdentityRole<long> role, CancellationToken cancellationToken) => Task.FromResult(role.ConcurrencyStamp);

    public Task SetConcurrencyStampAsync(IdentityRole<long> role, string stamp, CancellationToken cancellationToken)
    {
        role.ConcurrencyStamp = stamp;
        return Task.CompletedTask;
    }

    public async Task AddClaimsAsync(IdentityRole<long> role, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO mb_role_claims (role_id, claim_type, claim_value) VALUES (@RoleId, @ClaimType, @ClaimValue);",
            claims.Select(c => new { RoleId = role.Id, ClaimType = c.Type, ClaimValue = c.Value }),
            cancellationToken: cancellationToken));
    }

    public async Task<IList<Claim>> GetClaimsAsync(IdentityRole<long> role, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<(string ClaimType, string? ClaimValue)>(new CommandDefinition(
            "SELECT claim_type AS ClaimType, claim_value AS ClaimValue FROM mb_role_claims WHERE role_id = @id",
            new { id = role.Id }, cancellationToken: cancellationToken));
        return rows.Select(r => new Claim(r.ClaimType, r.ClaimValue ?? string.Empty)).ToList();
    }

    public async Task RemoveClaimsAsync(IdentityRole<long> role, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        foreach (var claim in claims)
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM mb_role_claims WHERE role_id = @roleId AND claim_type = @type AND claim_value = @value",
                new { roleId = role.Id, type = claim.Type, value = claim.Value }, cancellationToken: cancellationToken));
    }
}
