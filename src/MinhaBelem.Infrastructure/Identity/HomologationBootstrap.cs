using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Identity;
using MinhaBelem.Core.Security;
using MySqlConnector;

namespace MinhaBelem.Infrastructure.Identity;

/// <summary>Explicit local command only. Never registered as a startup task.</summary>
public sealed class HomologationBootstrap(MySqlDataSource data, UserManager<ApplicationUser> users)
{
    public async Task<string> RunAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var cs = new MySqlConnectionStringBuilder(data.ConnectionString);
        if (cs.Server is not ("localhost" or "127.0.0.1")) throw new InvalidOperationException("Somente MySQL local autorizado.");
        // Protect the directory BEFORE writing any credentials; no inherited permissions.
        var directory = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MNSOFT", "Homologacao", Guid.NewGuid().ToString("N")));
        directory.Create();
        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(true, false);
        acl.SetOwner(WindowsIdentity.GetCurrent().User!);
        acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(acl);
        await using var db = await data.OpenConnectionAsync(ct);
        if (await db.ExecuteScalarAsync<int>("SELECT GET_LOCK('mb_homologation_bootstrap',10)") != 1) throw new InvalidOperationException("Bootstrap já em execução.");
        var report = new List<object>();
        try
        {
            foreach (var (email, role) in new[] { ("superadmin@minhabelem.com.br", Roles.SuperAdmin), ("editor.teste@minhabelem.com.br", Roles.Editor), ("moderador.teste@minhabelem.com.br", Roles.Moderator) })
            {
                var existing = await users.FindByEmailAsync(email);
                if (existing is not null) { report.Add(new { Login = email, Perfil = role, Situacao = "Existente: preservado sem alterar senha, bloqueio ou permissões", MFAObrigatorio = true }); continue; }
                // Only seed defaults for a NEW role, preserving administrator revocations on existing roles.
                var roleId = await db.ExecuteScalarAsync<long?>("SELECT id FROM mb_roles WHERE normalized_name=@name", new { name = role.ToUpperInvariant() });
                if (roleId is null)
                {
                    roleId = await db.ExecuteScalarAsync<long>("INSERT INTO mb_roles(name,normalized_name,concurrency_stamp) VALUES(@role,@name,@stamp); SELECT LAST_INSERT_ID()", new { role, name = role.ToUpperInvariant(), stamp = Guid.NewGuid().ToString("N") });
                    foreach (var permission in Permissions.ByRole[role])
                    {
                        await db.ExecuteAsync("INSERT IGNORE INTO mb_permissions(code,description) VALUES(@permission,@description)", new { permission, description = Permissions.Descriptions[permission] });
                        await db.ExecuteAsync("INSERT IGNORE INTO mb_role_permissions(role_id,permission_code) VALUES(@roleId,@permission)", new { roleId, permission });
                    }
                }
                var password = "Aa9!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(27));
                var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Homologação " + role, EmailConfirmed = true, Enabled = true, LockoutEnabled = true };
                var created = await users.CreateAsync(user, password);
                if (!created.Succeeded) throw new InvalidOperationException("Falha no cadastro de homologação: " + string.Join(",", created.Errors.Select(x => x.Code)));
                // Fail closed until role assignment and first-access restriction are persisted.
                user.MustChangePassword = true;
                if (!(await users.UpdateAsync(user)).Succeeded || !(await users.AddToRoleAsync(user, role)).Succeeded)
                { user.Enabled = false; await users.UpdateAsync(user); throw new InvalidOperationException("Conta incompleta bloqueada."); }
                var persisted = await users.FindByEmailAsync(email);
                var loginOk = persisted is not null && await users.CheckPasswordAsync(persisted, password) && await users.IsInRoleAsync(persisted, role);
                await db.ExecuteAsync("INSERT INTO mb_audit_logs(actor_id,action,entity_type,entity_id,details) VALUES(@id,'user.homologation_created','user',@id,JSON_OBJECT('role',@role,'credentialVerified',@loginOk))", new { id = user.Id, role, loginOk });
                report.Add(new { Login = email, Perfil = role, SenhaTemporaria = password, Situacao = "Persistido", MFAObrigatorio = true, TrocaSenhaObrigatoria = true, AutenticacaoPersistida = loginOk });
            }
            var path = Path.Combine(directory.FullName, "credenciais-temporarias.json");
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(file, new { ExpiraEmUtc = DateTime.UtcNow.AddHours(24), Instrucao = "Leia localmente uma única vez, configure a senha e MFA, e apague este relatório temporário.", Contas = report }, new JsonSerializerOptions { WriteIndented = true }, ct);
            return path;
        }
        finally { await db.ExecuteAsync("SELECT RELEASE_LOCK('mb_homologation_bootstrap')"); }
    }
}
