using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Entities;
using MinhaBelem.Core.Security;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.Identity;

/// <summary>Opt-in local operator command. Never a hosted service or HTTP endpoint.</summary>
public sealed class LocalCredentialRecovery(MySqlDataSource data, UserManager<ApplicationUser> users,
    IAdministrationRepository admin, IAuditRepository audit, IConfiguration config, IHostEnvironment environment)
{
    public static readonly IReadOnlyDictionary<string,string> Accounts = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
    {
        ["superadmin@minhabelem.com.br"] = Roles.SuperAdmin,
        ["editor.teste@minhabelem.com.br"] = Roles.Editor,
        ["moderador.teste@minhabelem.com.br"] = Roles.Moderator
    };
    public async Task<string> RunAsync(bool confirmed, IReadOnlyList<string>? selected = null, CancellationToken ct = default)
    {
        var cs = new MySqlConnectionStringBuilder(data.ConnectionString);
        if (!OperatingSystem.IsWindows() || !environment.IsDevelopment() || !confirmed ||
            !config.GetValue<bool>("Homologation:AllowCredentialRecovery") || cs.Server is not ("localhost" or "127.0.0.1") ||
            string.IsNullOrWhiteSpace(cs.Database) || config["Homologation:RecoveryDatabaseReference"] != AuthenticationDiagnostics.DatabaseReference(data.ConnectionString))
            throw new InvalidOperationException("Recuperação exige Windows Development local, autorização, confirmação e referência exata do banco de homologação.");
        var emails = selected ?? Accounts.Keys.ToArray();
        if (emails.Count is < 1 or > 3 || emails.Distinct(StringComparer.OrdinalIgnoreCase).Count() != emails.Count || emails.Any(x=>!Accounts.ContainsKey(x)))
            throw new InvalidOperationException("Selecione somente as contas de homologação permitidas.");
        // Preflight the whole request before any password is changed. Never create/enable accounts or change roles/MFA.
        var targets = new List<ApplicationUser>();
        foreach (var email in emails)
        {
            var user = await users.FindByEmailAsync(email);
            if (user is null || !user.Enabled || !await users.IsInRoleAsync(user, Accounts[email]))
                throw new InvalidOperationException("Conta ausente, desabilitada ou com perfil divergente. Nenhuma redefinição foi iniciada.");
            try
            {
                if (user.TwoFactorEnabled && string.IsNullOrWhiteSpace(await users.GetAuthenticatorKeyAsync(user)))
                    throw new InvalidOperationException("Autenticador indisponível. Restaure as chaves/configuração antes de recuperar a senha.");
            }
            catch (CryptographicException) { throw new InvalidOperationException("Chaves de proteção indisponíveis. Nenhuma redefinição foi iniciada; restaure o chaveiro original."); }
            targets.Add(user);
        }
        var directory = Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MNSOFT", "Homologacao", "Recovery", Guid.NewGuid().ToString("N")));
        var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true,false);
        var owner = WindowsIdentity.GetCurrent().User!; acl.SetOwner(owner);
        acl.AddAccessRule(new FileSystemAccessRule(owner,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        directory.SetAccessControl(acl);
        var reportPath = Path.Combine(directory.FullName,"credenciais-temporarias.json");
        var report = new List<object>(); var expires = DateTime.UtcNow.AddHours(24);
        foreach (var target in targets)
        {
            var user = (await users.FindByIdAsync(target.Id.ToString()))!;
            if (user is null || !user.Enabled || !await users.IsInRoleAsync(user,Accounts[target.Email!]))
                throw new InvalidOperationException("A conta mudou após a conferência. Recuperação interrompida sem alterar essa conta.");
            var password = "Aa9!"+Convert.ToBase64String(RandomNumberGenerator.GetBytes(30));
            var token = await users.GeneratePasswordResetTokenAsync(user);
            user.MustChangePassword = true; user.LockoutEnd = null; user.AccessFailedCount = 0;
            var result = await users.ResetPasswordAsync(user,token,password);
            if (!result.Succeeded) throw new InvalidOperationException("Recuperação interrompida: "+string.Join(",",result.Errors.Select(x=>x.Code)));
            var persisted = (await users.FindByIdAsync(user.Id.ToString()))!;
            if (!persisted.MustChangePassword || !await users.CheckPasswordAsync(persisted,password)) throw new InvalidOperationException("A redefinição não pôde ser confirmada.");
            report.Add(new { Login=user.Email,Perfil=Accounts[user.Email!],SenhaTemporaria=password,Situacao="Persistido",MFAObrigatorio=true,TrocaSenhaObrigatoria=true });
            await File.WriteAllTextAsync(reportPath,JsonSerializer.Serialize(new { ExpiraEmUtc=expires,Contas=report },new JsonSerializerOptions { WriteIndented=true }),ct);
            // Keep the verified credential available even if subsequent session/audit I/O fails.
            await admin.RevokeAllSessionsAsync(user.Id,ct);
            await audit.WriteAsync(new AuditEntry { Action="user.local_password_recovered",EntityType="user",EntityId=user.Id,DetailsJson="{\"mfaPreserved\":true,\"mustChangePassword\":true}" },ct);
        }
        return reportPath;
    }
}
