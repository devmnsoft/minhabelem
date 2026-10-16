using System.Globalization;
using System.Security.Cryptography;
using Dapper;
using Microsoft.AspNetCore.Identity;
using MySqlConnector;
using Microsoft.AspNetCore.DataProtection;

namespace MinhaBelem.Infrastructure.Identity;

/// <summary>DTO intermediário: MySQL devolve DateTime, Identity trabalha com DateTimeOffset.</summary>
internal sealed class UserRow
{
    public long Id { get; set; }
    public string? UserName { get; set; }
    public string? NormalizedUserName { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? PasswordHash { get; set; }
    public string? SecurityStamp { get; set; }
    public string? ConcurrencyStamp { get; set; }
    public bool LockoutEnabled { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public int AccessFailedCount { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool MfaEnabled { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTime? LockoutDate { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class MySqlUserStore(MySqlDataSource dataSource, IDataProtectionProvider protection)
    : IUserPasswordStore<ApplicationUser>, IUserEmailStore<ApplicationUser>, IUserSecurityStampStore<ApplicationUser>,
      IUserLockoutStore<ApplicationUser>, IUserTwoFactorStore<ApplicationUser>, IUserPhoneNumberStore<ApplicationUser>,
      IUserClaimStore<ApplicationUser>, IUserRoleStore<ApplicationUser>, IUserLoginStore<ApplicationUser>, IUserAuthenticationTokenStore<ApplicationUser>,
      IUserAuthenticatorKeyStore<ApplicationUser>, IUserTwoFactorRecoveryCodeStore<ApplicationUser>
{
    private readonly IDataProtector protector = protection.CreateProtector("MinhaBelem.Identity.Tokens.v1");
    private const string SelectColumns = """
        id AS Id, user_name AS UserName, normalized_user_name AS NormalizedUserName, email AS Email,
        normalized_email AS NormalizedEmail, email_confirmed AS EmailConfirmed, password_hash AS PasswordHash,
        security_stamp AS SecurityStamp, concurrency_stamp AS ConcurrencyStamp,
        lockout_enabled AS LockoutEnabled, lockout_end AS LockoutEnd, access_failed_count AS AccessFailedCount,
        display_name AS DisplayName, enabled AS Enabled, mfa_enabled AS MfaEnabled, phone_number AS PhoneNumber,
        phone_number_confirmed AS PhoneNumberConfirmed, two_factor_enabled AS TwoFactorEnabled,
        lockout_date AS LockoutDate, password_changed_at AS PasswordChangedAt,
        last_login_at AS LastLoginAt, created_at AS CreatedAt
        """;

    private static ApplicationUser Map(UserRow row) => new()
    {
        Id = row.Id,
        UserName = row.UserName,
        NormalizedUserName = row.NormalizedUserName,
        Email = row.Email,
        NormalizedEmail = row.NormalizedEmail,
        EmailConfirmed = row.EmailConfirmed,
        PasswordHash = row.PasswordHash,
        SecurityStamp = row.SecurityStamp,
        ConcurrencyStamp = row.ConcurrencyStamp,
        LockoutEnabled = row.LockoutEnabled,
        LockoutEnd = ToOffset(row.LockoutEnd),
        AccessFailedCount = row.AccessFailedCount,
        DisplayName = row.DisplayName,
        Enabled = row.Enabled,
        MfaEnabled = row.MfaEnabled,
        PhoneNumber = row.PhoneNumber,
        PhoneNumberConfirmed = row.PhoneNumberConfirmed,
        TwoFactorEnabled = row.TwoFactorEnabled,
        LockoutDate = row.LockoutDate,
        PasswordChangedAt = row.PasswordChangedAt,
        LastLoginAt = row.LastLoginAt,
        CreatedAt = row.CreatedAt
    };

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static DateTime? ToUtc(DateTimeOffset? value) =>
        value?.UtcDateTime;

    private async Task<MySqlConnection> OpenAsync(CancellationToken ct)
        => (MySqlConnection)await dataSource.OpenConnectionAsync(ct);

    public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.SecurityStamp ??= NewStamp();
        user.ConcurrencyStamp = NewStamp();
        user.CreatedAt = user.CreatedAt == default ? DateTime.UtcNow : user.CreatedAt;

        await using var connection = await OpenAsync(cancellationToken);
        try
        {
            var id = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
                """
                INSERT INTO mb_users
                  (user_name, normalized_user_name, email, normalized_email, email_confirmed, password_hash,
                   security_stamp, concurrency_stamp, lockout_enabled, access_failed_count,
                   display_name, enabled, mfa_enabled, phone_number, phone_number_confirmed,
                   two_factor_enabled, password_changed_at, last_login_at, created_at)
                VALUES
                  (@UserName, @NormalizedUserName, @Email, @NormalizedEmail, @EmailConfirmed, @PasswordHash,
                   @SecurityStamp, @ConcurrencyStamp, @LockoutEnabled, @AccessFailedCount,
                   @DisplayName, @Enabled, @MfaEnabled, @PhoneNumber, @PhoneNumberConfirmed,
                   @TwoFactorEnabled, @PasswordChangedAt, @LastLoginAt, @CreatedAt);
                SELECT LAST_INSERT_ID();
                """, new
                {
                    user.UserName, user.NormalizedUserName, user.Email, user.NormalizedEmail,
                    user.EmailConfirmed, user.PasswordHash, user.SecurityStamp, user.ConcurrencyStamp,
                    user.LockoutEnabled, user.AccessFailedCount, user.DisplayName, user.Enabled,
                    user.MfaEnabled, user.PhoneNumber, user.PhoneNumberConfirmed,
                    user.TwoFactorEnabled,
                    PasswordChangedAt = user.PasswordHash is null ? (DateTime?)null : user.PasswordChangedAt ?? DateTime.UtcNow,
                    LastLoginAt = user.LastLoginAt,
                    user.CreatedAt
                }, cancellationToken: cancellationToken));

            if (id is null or <= 0)
                return IdentityResult.Failed(new IdentityError { Code = "InsertFailed", Description = "Não foi possível criar o usuário." });

            user.Id = id.Value;
            return IdentityResult.Success;
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DuplicateUser",
                Description = ex.Message.Contains("uq_users_normalized_email", StringComparison.OrdinalIgnoreCase)
                    ? "Já existe um usuário com este e-mail."
                    : "Já existe um usuário com este nome."
            });
        }
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        // Concorrência otimista: o carimbo carregado com o usuário precisa casar com o do banco.
        var originalStamp = user.ConcurrencyStamp;
        user.ConcurrencyStamp = NewStamp();
        await using var connection = await OpenAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE mb_users SET
                user_name = @UserName, normalized_user_name = @NormalizedUserName,
                email = @Email, normalized_email = @NormalizedEmail, email_confirmed = @EmailConfirmed,
                password_hash = @PasswordHash, security_stamp = @SecurityStamp,
                concurrency_stamp = @ConcurrencyStamp, lockout_enabled = @LockoutEnabled,
                lockout_end = @LockoutEnd, access_failed_count = @AccessFailedCount,
                display_name = @DisplayName, enabled = @Enabled, mfa_enabled = @MfaEnabled,
                phone_number = @PhoneNumber, phone_number_confirmed = @PhoneNumberConfirmed,
                two_factor_enabled = @TwoFactorEnabled, lockout_date = @LockoutDate,
                password_changed_at = @PasswordChangedAt, last_login_at = @LastLoginAt
            WHERE id = @Id AND concurrency_stamp = @OriginalConcurrencyStamp
            """,
            new
            {
                user.Id, user.UserName, user.NormalizedUserName, user.Email, user.NormalizedEmail,
                user.EmailConfirmed, user.PasswordHash, user.SecurityStamp, user.ConcurrencyStamp,
                user.LockoutEnabled, LockoutEnd = ToUtc(user.LockoutEnd), user.AccessFailedCount,
                user.DisplayName, user.Enabled, user.MfaEnabled, user.PhoneNumber,
                user.PhoneNumberConfirmed, user.TwoFactorEnabled, user.LockoutDate,
                user.PasswordChangedAt, user.LastLoginAt,
                OriginalConcurrencyStamp = originalStamp
            }, cancellationToken: cancellationToken));

        if (affected == 1) return IdentityResult.Success;

        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM mb_users WHERE id = @id", new { user.Id }, cancellationToken: cancellationToken));
        return exists == 1
            ? IdentityResult.Failed(new IdentityError { Code = "ConcurrencyFailure", Description = "O registro foi alterado por outra pessoa. Recarregue a tela." })
            : IdentityResult.Failed(new IdentityError { Code = "NotFound", Description = "Usuário não encontrado." });
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM mb_users WHERE id = @id", new { user.Id }, cancellationToken: cancellationToken));
        return IdentityResult.Success;
    }

    public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        FindOneAsync("id", long.Parse(userId, CultureInfo.InvariantCulture), cancellationToken);

    public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        FindOneAsync("normalized_user_name", normalizedUserName, cancellationToken);

    public Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        FindOneAsync("normalized_email", normalizedEmail, cancellationToken);

    private async Task<ApplicationUser?> FindOneAsync(string column, object value, CancellationToken ct)
    {
        var allowed = new[] { "id", "normalized_user_name", "normalized_email" };
        if (!allowed.Contains(column)) throw new ArgumentOutOfRangeException(nameof(column));
        await using var connection = await OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM mb_users WHERE {column} = @value LIMIT 1", new { value }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id.ToString(CultureInfo.InvariantCulture));

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.UserName);

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        user.NormalizedUserName = userName?.ToUpperInvariant();
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Email);

    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email;
        user.NormalizedEmail = email?.ToUpperInvariant();
        return Task.CompletedTask;
    }

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.EmailConfirmed);
    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash);
    public Task SetPasswordHashAsync(ApplicationUser user, string? hash, CancellationToken cancellationToken)
    {
        user.PasswordHash = hash;
        user.PasswordChangedAt = hash is null ? null : DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.SecurityStamp);
    public Task SetSecurityStampAsync(ApplicationUser user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnd);
    public Task SetLockoutEndDateAsync(ApplicationUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount++;
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnabled);
    public Task SetLockoutEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    public async Task<bool> IsLockedOutAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        user.LockoutEnabled && user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow;

    public Task<bool> GetTwoFactorEnabledAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.TwoFactorEnabled);
    public Task SetTwoFactorEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.TwoFactorEnabled = enabled;
        user.MfaEnabled = user.MfaEnabled || enabled;
        return Task.CompletedTask;
    }

    public Task<string?> GetPhoneNumberAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.PhoneNumber);
    public Task SetPhoneNumberAsync(ApplicationUser user, string? phone, CancellationToken cancellationToken)
    {
        user.PhoneNumber = phone;
        return Task.CompletedTask;
    }

    public Task<bool> GetPhoneNumberConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.PhoneNumberConfirmed);
    public Task SetPhoneNumberConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.PhoneNumberConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    public Task<string?> GetConcurrencyStampAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.ConcurrencyStamp);
    public Task SetConcurrencyStampAsync(ApplicationUser user, string stamp, CancellationToken cancellationToken)
    {
        user.ConcurrencyStamp = stamp;
        return Task.CompletedTask;
    }

    // Claims
    public async Task<IList<Claim>> GetClaimsAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<ClaimRow>(new CommandDefinition(
            "SELECT id AS Id, user_id AS UserId, claim_type AS ClaimType, claim_value AS ClaimValue FROM mb_user_claims WHERE user_id = @id",
            new { user.Id }, cancellationToken: cancellationToken));
        return rows.Select(r => new Claim(r.ClaimType, r.ClaimValue ?? string.Empty)).ToList();
    }

    public async Task AddClaimsAsync(ApplicationUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO mb_user_claims (user_id, claim_type, claim_value) VALUES (@UserId, @ClaimType, @ClaimValue);",
            claims.Select(c => new { UserId = user.Id, ClaimType = c.Type, ClaimValue = c.Value }),
            cancellationToken: cancellationToken));
    }

    public async Task ReplaceClaimAsync(ApplicationUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE mb_user_claims SET claim_value = @Value WHERE user_id = @UserId AND claim_type = @Type AND claim_value = @Old",
            new { Value = newClaim.Value, UserId = user.Id, Type = claim.Type, Old = claim.Value }, cancellationToken: cancellationToken));
    }

    public async Task RemoveClaimsAsync(ApplicationUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        foreach (var claim in claims)
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM mb_user_claims WHERE user_id = @UserId AND claim_type = @Type AND claim_value = @Value",
                new { UserId = user.Id, Type = claim.Type, Value = claim.Value }, cancellationToken: cancellationToken));
    }

    // Roles
    public async Task AddToRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT IGNORE INTO mb_user_roles (user_id, role_id)
            SELECT @userId, id FROM mb_roles WHERE normalized_name = @role
            """, new { userId = user.Id, role = roleName.ToUpperInvariant() }, cancellationToken: cancellationToken));
    }

    public async Task RemoveFromRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE ur FROM mb_user_roles ur JOIN mb_roles r ON r.id = ur.role_id WHERE ur.user_id = @userId AND r.normalized_name = @role",
            new { userId = user.Id, role = roleName.ToUpperInvariant() }, cancellationToken: cancellationToken));
    }

    public async Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT r.name FROM mb_roles r JOIN mb_user_roles ur ON ur.role_id = r.id WHERE ur.user_id = @userId",
            new { userId = user.Id }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<bool> IsInRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(*) FROM mb_user_roles ur JOIN mb_roles r ON r.id = ur.role_id
            WHERE ur.user_id = @userId AND r.normalized_name = @role
            """, new { userId = user.Id, role = roleName.ToUpperInvariant() }, cancellationToken: cancellationToken)) > 0;
    }

    // Logins
    public async Task AddLoginAsync(ApplicationUser user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO mb_user_logins (login_provider, provider_key, provider_display_name, user_id)
            VALUES (@LoginProvider, @ProviderKey, @ProviderDisplayName, @UserId)
            """, new { login.LoginProvider, login.ProviderKey, login.ProviderDisplayName, UserId = user.Id }, cancellationToken: cancellationToken));
    }

    public async Task RemoveLoginAsync(ApplicationUser user, string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM mb_user_logins WHERE user_id = @userId AND login_provider = @loginProvider AND provider_key = @providerKey",
            new { userId = user.Id, loginProvider, providerKey }, cancellationToken: cancellationToken));
    }

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<(string LoginProvider, string ProviderKey, string? Display)>(new CommandDefinition(
            "SELECT login_provider AS LoginProvider, provider_key AS ProviderKey, provider_display_name AS Display FROM mb_user_logins WHERE user_id = @userId",
            new { userId = user.Id }, cancellationToken: cancellationToken));
        return rows.Select(r => new UserLoginInfo(r.LoginProvider, r.ProviderKey, r.Display)).ToList();
    }

    // Tokens
    public async Task SetTokenAsync(ApplicationUser user, string loginProvider, string name, string? value, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO mb_user_tokens (user_id, login_provider, name, value_cipher)
            VALUES (@userId, @loginProvider, @name, @value)
            ON DUPLICATE KEY UPDATE value_cipher = VALUES(value_cipher)
            """, new { userId = user.Id, loginProvider, name, value = protector.Protect(value ?? string.Empty) }, cancellationToken: cancellationToken));
    }

    public async Task RemoveTokenAsync(ApplicationUser user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM mb_user_tokens WHERE user_id = @userId AND login_provider = @loginProvider AND name = @name",
            new { userId = user.Id, loginProvider, name }, cancellationToken: cancellationToken));
    }

    public async Task<string?> GetTokenAsync(ApplicationUser user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var encrypted = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value_cipher FROM mb_user_tokens WHERE user_id = @userId AND login_provider = @loginProvider AND name = @name",
            new { userId = user.Id, loginProvider, name }, cancellationToken: cancellationToken));
        return encrypted is null ? null : protector.Unprotect(encrypted);
    }

    public Task SetAuthenticatorKeyAsync(ApplicationUser user, string key, CancellationToken ct) => SetTokenAsync(user, "Identity", "AuthenticatorKey", key, ct);
    public Task<string?> GetAuthenticatorKeyAsync(ApplicationUser user, CancellationToken ct) => GetTokenAsync(user, "Identity", "AuthenticatorKey", ct);
    public Task ReplaceCodesAsync(ApplicationUser user, IEnumerable<string> codes, CancellationToken ct) => SetTokenAsync(user, "Identity", "RecoveryCodes", string.Join(';', codes), ct);
    public async Task<int> CountCodesAsync(ApplicationUser user, CancellationToken ct) =>
        (await GetTokenAsync(user, "Identity", "RecoveryCodes", ct))?.Split(';', StringSplitOptions.RemoveEmptyEntries).Length ?? 0;
    public async Task<bool> RedeemCodeAsync(ApplicationUser user, string code, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var encrypted = await db.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT value_cipher FROM mb_user_tokens WHERE user_id=@id AND login_provider='Identity' AND name='RecoveryCodes' FOR UPDATE", new { id = user.Id }, tx, cancellationToken: ct));
        if (encrypted is null) return false;
        var codes = protector.Unprotect(encrypted).Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!codes.Remove(code)) return false;
        await db.ExecuteAsync(new CommandDefinition("UPDATE mb_user_tokens SET value_cipher=@value WHERE user_id=@id AND login_provider='Identity' AND name='RecoveryCodes'", new { id = user.Id, value = protector.Protect(string.Join(';', codes)) }, tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return true;
    }


    public void Dispose() { }
    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedEmail);
    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash is not null);
    public async Task<ApplicationUser?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var id = await connection.ExecuteScalarAsync<long?>(new CommandDefinition("SELECT user_id FROM mb_user_logins WHERE login_provider = @loginProvider AND provider_key = @providerKey", new { loginProvider, providerKey }, cancellationToken: cancellationToken));
        return id is null ? null : await FindByIdAsync(id.Value.ToString(), cancellationToken);
    }
    public async Task<IList<ApplicationUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var ids = await connection.QueryAsync<long>(new CommandDefinition("SELECT DISTINCT user_id FROM mb_user_claims WHERE claim_type = @Type AND claim_value = @Value", new { claim.Type, claim.Value }, cancellationToken: cancellationToken));
        var users = new List<ApplicationUser>();
        foreach (var id in ids) if (await FindByIdAsync(id.ToString(), cancellationToken) is { } user) users.Add(user);
        return users;
    }
    public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var ids = await connection.QueryAsync<long>(new CommandDefinition("SELECT ur.user_id FROM mb_user_roles ur JOIN mb_roles r ON r.id = ur.role_id WHERE r.normalized_name = @name", new { name = roleName }, cancellationToken: cancellationToken));
        var users = new List<ApplicationUser>();
        foreach (var id in ids) if (await FindByIdAsync(id.ToString(), cancellationToken) is { } user) users.Add(user);
        return users;
    }

    private sealed class ClaimRow
    {
        public int Id { get; set; }
        public long UserId { get; set; }
        public string ClaimType { get; set; } = string.Empty;
        public string? ClaimValue { get; set; }
    }
}
