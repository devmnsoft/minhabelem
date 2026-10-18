using System.Net;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Security;
using MinhaBelem.Infrastructure.Identity;
using MySqlConnector;

namespace MinhaBelem.Tests;

[Collection("MySql")]
public sealed class AuthenticationDiagnosticsTests
{
    [MySqlFact]
    public async Task LocalSnapshotNamesMissingAccountsAndReportsStateRolesAndEffectivePermissionsWithoutSecrets()
    {
        var fixture=new PortalFixture(); await fixture.InitializeAsync();
        try
        {
            using var scope=fixture.Factory.Services.CreateScope();
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var data=scope.ServiceProvider.GetRequiredService<MySqlDataSource>();
            using(var empty=JsonDocument.Parse(JsonSerializer.Serialize(await AuthenticationDiagnostics.ReadLocalAsync(data,environment:"Development"))))
            {
                Assert.Equal(3,empty.RootElement.GetProperty("accounts").GetArrayLength());
                Assert.All(empty.RootElement.GetProperty("accounts").EnumerateArray(),x=>Assert.Equal("account_absent",x.GetProperty("status").GetString()));
            }
            var password="Aa1!"+Guid.NewGuid().ToString("N");
            foreach(var (email,role) in LocalCredentialRecovery.Accounts)
            {
                var user=new ApplicationUser{Email=email,UserName=email,Enabled=role!=Roles.Editor,EmailConfirmed=true,LockoutEnabled=true,MustChangePassword=role==Roles.SuperAdmin};
                Assert.True((await users.CreateAsync(user,password)).Succeeded);
                Assert.True((await users.AddToRoleAsync(user,role)).Succeeded);
                if(role==Roles.Moderator) Assert.True((await users.SetLockoutEndDateAsync(user,DateTimeOffset.UtcNow.AddMinutes(15))).Succeeded);
            }
            var json=JsonSerializer.Serialize(await AuthenticationDiagnostics.ReadLocalAsync(data,environment:"Development"));
            Assert.False(json.Contains(password,StringComparison.Ordinal));
            using var snapshot=JsonDocument.Parse(json);var root=snapshot.RootElement;
            Assert.Equal("Development",root.GetProperty("environment").GetString());
            Assert.Equal(new MySqlConnectionStringBuilder(fixture.ConnectionString).Database,root.GetProperty("database").GetString());
            Assert.False(root.TryGetProperty("connectionString",out _));
            var accounts=root.GetProperty("accounts").EnumerateArray().ToArray();
            Assert.Equal("password_change_required",accounts.Single(x=>x.GetProperty("expectedRole").GetString()==Roles.SuperAdmin).GetProperty("status").GetString());
            Assert.Equal("disabled",accounts.Single(x=>x.GetProperty("expectedRole").GetString()==Roles.Editor).GetProperty("status").GetString());
            Assert.Equal("locked_out",accounts.Single(x=>x.GetProperty("expectedRole").GetString()==Roles.Moderator).GetProperty("status").GetString());
            Assert.All(accounts,account=>
            {
                Assert.True(account.GetProperty("exists").GetBoolean());Assert.True(account.GetProperty("expectedRolePresent").GetBoolean());
                Assert.Contains(account.GetProperty("permissions").EnumerateArray(),p=>p.GetString()==Permissions.DashboardRead);
            });
            var moderator=(await users.FindByEmailAsync("moderador.teste@minhabelem.com.br"))!;
            Assert.True((await users.SetLockoutEnabledAsync(moderator,false)).Succeeded);
            Assert.False(await users.IsLockedOutAsync(moderator));
            using var unlocked=JsonDocument.Parse(JsonSerializer.Serialize(await AuthenticationDiagnostics.ReadLocalAsync(data,environment:"Development")));
            Assert.False(unlocked.RootElement.GetProperty("accounts").EnumerateArray().Single(x=>x.GetProperty("expectedRole").GetString()==Roles.Moderator).GetProperty("lockedOut").GetBoolean());
        }
        finally { await fixture.DisposeAsync(); }
    }

    [MySqlFact]
    public async Task HttpFailuresHaveGenericPublicMessagesAndDistinctSecretFreeAdministrativeReasons()
    {
        var fixture=new PortalFixture();await fixture.InitializeAsync();
        try
        {
            using var scope=fixture.Factory.Services.CreateScope();var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var password="Aa1!"+Guid.NewGuid().ToString("N");
            var enabled=new ApplicationUser{Email="enabled@example.com",UserName="enabled@example.com",Enabled=true,EmailConfirmed=true,LockoutEnabled=true};
            var disabled=new ApplicationUser{Email="disabled@example.com",UserName="disabled@example.com",Enabled=false,EmailConfirmed=true};
            Assert.True((await users.CreateAsync(enabled,password)).Succeeded);Assert.True((await users.CreateAsync(disabled,password)).Succeeded);
            using var client=fixture.Client();
            foreach(var email in new[]{"absent@example.com",disabled.Email!,enabled.Email!})
            {
                var response=await Login(client,email,"Wrong-Aa1!Password");
                Assert.Equal(HttpStatusCode.OK,response.StatusCode);
                var html=WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
                Assert.Contains("Não foi possível entrar. Confira os dados ou aguarde antes de tentar novamente.",html);
                Assert.False(html.Contains("invalid_password",StringComparison.Ordinal));Assert.False(html.Contains("account_absent",StringComparison.Ordinal));
            }
            enabled=(await users.FindByIdAsync(enabled.Id.ToString()))!;
            Assert.True((await users.SetLockoutEndDateAsync(enabled,DateTimeOffset.UtcNow.AddMinutes(15))).Succeeded);
            await Login(client,enabled.Email!,password);
            Assert.True((await users.SetLockoutEndDateAsync(enabled,DateTimeOffset.UtcNow.AddSeconds(-1))).Succeeded);
            Assert.True((await users.ResetAuthenticatorKeyAsync(enabled)).Succeeded);Assert.True((await users.SetTwoFactorEnabledAsync(enabled,true)).Succeeded);
            Assert.Equal(HttpStatusCode.Redirect,(await Login(client,enabled.Email!,password)).StatusCode);
            await PortalFixture.Post(client,"/conta/verificar","/conta/verificar",new(){["code"]="invalid"});
            await using var db=await fixture.OpenAsync();
            var details=(await db.QueryAsync<string>("SELECT details FROM mb_audit_logs WHERE action='auth.attempt' ORDER BY id")).ToArray();
            var reasons=details.Select(x=>{using var json=JsonDocument.Parse(x);return json.RootElement.GetProperty("reason").GetString();}).ToArray();
            foreach(var reason in new[]{"account_absent","disabled","invalid_password","locked_out","mfa_required","invalid_factor"}) Assert.Contains(reason,reasons);
            Assert.All(details,x=>
            {
                Assert.False(x.Contains(password,StringComparison.Ordinal));Assert.False(x.Contains("@example.com",StringComparison.Ordinal));
                using var json=JsonDocument.Parse(x);Assert.Equal(2,json.RootElement.EnumerateObject().Count());
            });
        }
        finally { await fixture.DisposeAsync(); }
    }

    private static Task<HttpResponseMessage> Login(HttpClient client,string email,string password)=>PortalFixture.Post(client,"/conta/login","/conta/login",new(){["Email"]=email,["Password"]=password});
}
