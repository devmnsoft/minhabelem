using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using MinhaBelem.Core.Security;
using MinhaBelem.Core.Text;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.Identity;

/// <summary>
/// Carga inicial: garante papéis/permissões, localities de referência e o superadministrador
/// inicial declarado em <c>Bootstrap:SuperAdmin</c>. Roda sempre; é idempotente.
/// </summary>
public sealed class IdentitySeeder(
    MySqlDataSource dataSource,
    UserManager<ApplicationUser> users,
    ILogger<IdentitySeeder> logger)
{
    public sealed record BootstrapOptions(string Email, string Password, string DisplayName);

    public async Task SeedAsync(BootstrapOptions? bootstrap, CancellationToken ct = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);

        foreach (var role in Roles.All)
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT IGNORE INTO mb_roles (name, normalized_name, concurrency_stamp) VALUES (@Name, @Normalized, @Stamp);",
                new { Name = role, Normalized = role.ToUpperInvariant(), Stamp = Guid.NewGuid().ToString("N") },
                cancellationToken: ct));

        foreach (var code in Permissions.All)
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT IGNORE INTO mb_permissions (code, description) VALUES (@Code, @Description);",
                new { Code = code, Description = Permissions.Descriptions.TryGetValue(code, out var d) ? d : code },
                cancellationToken: ct));

        var roleIds = (await connection.QueryAsync<(long Id, string Normalized)>(new CommandDefinition(
            "SELECT id AS Id, normalized_name AS Normalized FROM mb_roles", cancellationToken: ct)))
            .ToDictionary(r => r.Normalized, r => r.Id);

        foreach (var (role, codes) in Permissions.ByRole)
        {
            if (!roleIds.TryGetValue(role.ToUpperInvariant(), out var roleId)) continue;
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT IGNORE INTO mb_role_permissions (role_id, permission_code) SELECT @RoleId, @Code;",
                codes.Select(code => new { RoleId = roleId, Code = code }), cancellationToken: ct));
        }

        await SeedLocalitiesAsync(connection, ct);

        if (bootstrap is null || string.IsNullOrWhiteSpace(bootstrap.Email)) return;

        var normalized = bootstrap.Email.Trim().ToUpperInvariant();
        var existing = await users.FindByEmailAsync(normalized);
        if (existing is not null)
        {
            throw new InvalidOperationException("O usuário já existe. Bootstrap não altera nem reativa contas existentes.");
        }

        var user = new ApplicationUser
        {
            UserName = bootstrap.Email.Trim(),
            Email = bootstrap.Email.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(bootstrap.DisplayName) ? "Superadministrador" : bootstrap.DisplayName.Trim(),
            EmailConfirmed = true,
            Enabled = true,
            LockoutEnabled = true
        };
        var result = await users.CreateAsync(user, bootstrap.Password);
        if (!result.Succeeded)
        {
            logger.LogError("Falha ao criar o superadministrador inicial: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            throw new InvalidOperationException(
                "Não foi possível criar o superadministrador inicial: " + string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        var rolesResult = await users.AddToRolesAsync(user, [Roles.SuperAdmin]);
        if (!rolesResult.Succeeded) throw new InvalidOperationException("Não foi possível atribuir o perfil inicial.");
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO mb_audit_logs (actor_id, actor_name, action, entity_type, details)
            VALUES (@id, @name, 'user.bootstrap_created', 'user', @details)
            """,
            new { id = user.Id, name = user.DisplayName, details = "{\"source\":\"bootstrap\"}" },
            cancellationToken: ct));

        logger.LogWarning("Superadministrador inicial criado para {Email}. Troque a senha e ative o MFA imediatamente.", normalized);
    }

    private static async Task SeedLocalitiesAsync(MySqlConnection connection, CancellationToken ct)
    {
        foreach (var neighborhood in BelemGeography.BelemNeighborhoods)
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT IGNORE INTO mb_localities (name, slug, kind) VALUES (@Name, @Slug, 'neighborhood');",
                new { Name = neighborhood, Slug = HtmlText.Slugify(neighborhood) }, cancellationToken: ct));

        foreach (var municipality in BelemGeography.Municipalities)
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT IGNORE INTO mb_localities (name, slug, kind) VALUES (@Name, @Slug, 'municipality');",
                new { Name = municipality.Name, Slug = municipality.Slug }, cancellationToken: ct));
    }
}
