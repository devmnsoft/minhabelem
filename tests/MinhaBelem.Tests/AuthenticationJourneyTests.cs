using System.Net;
using System.Security.Claims;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MySqlConnector;
using MinhaBelem.Core.Abstractions;
using MinhaBelem.Core.Security;
using MinhaBelem.Infrastructure.Identity;

namespace MinhaBelem.Tests;

public sealed class MySqlTheoryAttribute : TheoryAttribute
{
    public MySqlTheoryAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MINHABELEM_TEST_MYSQL"))) Skip = "Requires a disposable local MySQL server."; }
}
public sealed class WindowsMySqlFactAttribute : FactAttribute
{
    public WindowsMySqlFactAttribute() { if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MINHABELEM_TEST_MYSQL"))) Skip="Requires Windows and disposable MySQL for protected local recovery reports."; }
}

[Collection("MySql")]
public sealed class AuthenticationJourneyTests(PortalFixture shared)
{
    [WindowsMySqlFact]
    public async Task ExplicitLocalRecoveryResetsOnlyAuthorizedAccountsAndPreservesMfa()
    {
        await using var lifetime=new AsyncFixture();await lifetime.Value.InitializeAsync();
        using var scope=lifetime.Value.Factory.Services.CreateScope();
        var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var data=scope.ServiceProvider.GetRequiredService<MySqlDataSource>();var admin=scope.ServiceProvider.GetRequiredService<IAdministrationRepository>();
        var audit=scope.ServiceProvider.GetRequiredService<IAuditRepository>();var environment=scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var originals=new Dictionary<string,(string Password,string Key,string Stamp,string Session)>();
        foreach(var (email,role) in LocalCredentialRecovery.Accounts)
        {
            var password="Aa1!"+Guid.NewGuid().ToString("N");
            var user=new ApplicationUser{Email=email,UserName=email,Enabled=true,EmailConfirmed=true,LockoutEnabled=true};
            Assert.True((await users.CreateAsync(user,password)).Succeeded);Assert.True((await users.AddToRoleAsync(user,role)).Succeeded);
            Assert.True((await users.ResetAuthenticatorKeyAsync(user)).Succeeded);Assert.True((await users.SetTwoFactorEnabledAsync(user,true)).Succeeded);
            var session=Guid.NewGuid().ToString("N");await admin.CreateSessionAsync(session,user.Id,DateTime.UtcNow.AddHours(1),default);
            originals[email]=(password,(await users.GetAuthenticatorKeyAsync(user))!,user.SecurityStamp!,session);
        }
        var unauthorized=new LocalCredentialRecovery(data,users,admin,audit,new ConfigurationBuilder().Build(),environment);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>unauthorized.RunAsync(true));
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Homologation:AllowCredentialRecovery"]="true",["Homologation:RecoveryDatabaseReference"]=AuthenticationDiagnostics.DatabaseReference(data.ConnectionString)
        }).Build();
        var recovery=new LocalCredentialRecovery(data,users,admin,audit,config,environment);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>recovery.RunAsync(false));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>recovery.RunAsync(true,["not-allowed@example.com"]));
        foreach(var (email,original) in originals) Assert.True(await users.CheckPasswordAsync((await users.FindByEmailAsync(email))!,original.Password));
        var path=await recovery.RunAsync(true);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var acl=new DirectoryInfo(Path.GetDirectoryName(path)!).GetAccessControl();
                Assert.True(acl.AreAccessRulesProtected);
                var owner=WindowsIdentity.GetCurrent().User!.Value;
                foreach (FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))
                    Assert.True(rule.IdentityReference.Value==owner);
            }
            using var document=JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.True(document.RootElement.GetProperty("ExpiraEmUtc").GetDateTime()>DateTime.UtcNow);
            var passwords=new HashSet<string>();
            foreach(var entry in document.RootElement.GetProperty("Contas").EnumerateArray())
            {
                var email=entry.GetProperty("Login").GetString()!;var password=entry.GetProperty("SenhaTemporaria").GetString()!;
                Assert.True(passwords.Add(password));var user=(await users.FindByEmailAsync(email))!;
                Assert.True(await users.CheckPasswordAsync(user,password));Assert.False(await users.CheckPasswordAsync(user,originals[email].Password));
                Assert.True(user.MustChangePassword);Assert.True(user.TwoFactorEnabled);Assert.True(user.SecurityStamp!=originals[email].Stamp);
                Assert.True(await users.GetAuthenticatorKeyAsync(user)==originals[email].Key);
                Assert.False(await admin.SessionActiveAsync(originals[email].Session,user.Id,default));
            }
        }
        finally { File.Delete(path); }
    }
    [MySqlFact]
    public async Task ConcurrencyFailureDoesNotMutateStampOrOverwriteTheNewPassword()
    {
        using var a = shared.Factory.Services.CreateScope(); using var b = shared.Factory.Services.CreateScope();
        var users = a.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var other = b.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var original = "Aa1!" + Guid.NewGuid().ToString("N"); var replacement = "Bb2!" + Guid.NewGuid().ToString("N");
        var user = new ApplicationUser { Email = Guid.NewGuid()+"@example.com", UserName = Guid.NewGuid()+"@example.com", Enabled = true };
        Assert.True((await users.CreateAsync(user, original)).Succeeded);
        var stale = (await users.FindByIdAsync(user.Id.ToString()))!;
        var fresh = (await other.FindByIdAsync(user.Id.ToString()))!;
        Assert.True((await other.ChangePasswordAsync(fresh, original, replacement)).Succeeded);
        var stamp = stale.ConcurrencyStamp; stale.DisplayName = "Concurrent test";
        var conflict = await users.UpdateAsync(stale);
        Assert.False(conflict.Succeeded); Assert.Contains(conflict.Errors, x=>x.Code=="ConcurrencyFailure");
        Assert.True(stale.ConcurrencyStamp == stamp);
        Assert.True(await users.CheckPasswordAsync((await users.FindByIdAsync(user.Id.ToString()))!, replacement));
    }

    [MySqlFact]
    public async Task PasswordAndFirstAccessFlagAreCommittedTogetherEvenWhenStampChangesInDatabase()
    {
        using var scope = shared.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var temporary = "Aa1!"+Guid.NewGuid().ToString("N"); var next = "Bb2!"+Guid.NewGuid().ToString("N");
        var email = Guid.NewGuid()+"@example.com";
        var user = new ApplicationUser { Email=email,UserName=email,Enabled=true,EmailConfirmed=true,MustChangePassword=true };
        Assert.True((await users.CreateAsync(user,temporary)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user,Roles.Editor)).Succeeded);
        using var client = shared.Client(); await Password(client,email,temporary);
        await using var db = await shared.OpenAsync();
        // Controlled fixture: emulate another writer advancing the version at the password commit boundary.
        await db.ExecuteAsync("""
            CREATE TRIGGER auth_fixture_stamp BEFORE UPDATE ON mb_users FOR EACH ROW
            BEGIN IF OLD.password_hash <> NEW.password_hash THEN SET NEW.concurrency_stamp=REPLACE(UUID(),'-',''); END IF; END
            """);
        try
        {
            var result = await PortalFixture.Post(client,"/conta/senha","/conta/perfil",new(){["CurrentPassword"]=temporary,["NewPassword"]=next});
            Assert.Equal(HttpStatusCode.Redirect,result.StatusCode);
            var persisted=(await users.FindByIdAsync(user.Id.ToString()))!;
            Assert.False(persisted.MustChangePassword); Assert.True(await users.CheckPasswordAsync(persisted,next));
        }
        finally { await db.ExecuteAsync("DROP TRIGGER auth_fixture_stamp"); }
    }
    [MySqlTheory]
    [InlineData(Roles.SuperAdmin, "superadmin@minhabelem.com.br")]
    [InlineData(Roles.Editor, "editor.teste@minhabelem.com.br")]
    [InlineData(Roles.Moderator, "moderador.teste@minhabelem.com.br")]
    public async Task BootstrapPasswordRotationMfaRevocationAndRestart(string role, string email)
    {
        await using var lifetime = new AsyncFixture();
        var fixture = lifetime.Value;
        await fixture.InitializeAsync();
        var keysPath = Path.Combine(Path.GetTempPath(), "minhabelem-auth-" + Guid.NewGuid().ToString("N"));
        await using var app = new PortalFactory(fixture.ConnectionString, keysPath);
        string temporary;
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (OperatingSystem.IsWindows())
            {
                var path = await scope.ServiceProvider.GetRequiredService<HomologationBootstrap>().RunAsync();
                try
                {
                    using var report = JsonDocument.Parse(await File.ReadAllTextAsync(path));
                    temporary = report.RootElement.GetProperty("Contas").EnumerateArray().Single(x => x.GetProperty("Login").GetString() == email).GetProperty("SenhaTemporaria").GetString()!;
                }
                finally { File.Delete(path); }
            }
            else
            {
                // The Windows protected-report command is not portable; seed its Identity state for Linux CI.
                temporary = "Aa9!" + Guid.NewGuid().ToString("N");
                var seeded = new ApplicationUser { Email = email, UserName = email, EmailConfirmed = true, Enabled = true, LockoutEnabled = true, MustChangePassword = true };
                Assert.True((await users.CreateAsync(seeded, temporary)).Succeeded);
                Assert.True((await users.AddToRoleAsync(seeded, role)).Succeeded);
            }
        }
        var jar = new Dictionary<string,string>();
        using var client = Client(app, jar);
        var login = await Password(client, email.ToUpperInvariant(), temporary);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.EndsWith("/conta/perfil", login.Headers.Location!.ToString());
        var replacement = "Bb8!" + Guid.NewGuid().ToString("N");
        string? originalStamp;
        using (var scope = app.Services.CreateScope()) originalStamp = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email))!.SecurityStamp;
        var changed = await PortalFixture.Post(client, "/conta/senha", "/conta/perfil", new() { ["CurrentPassword"] = temporary, ["NewPassword"] = replacement });
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.EndsWith("/conta/login", changed.Headers.Location!.ToString());
        long id;
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var persisted = (await users.FindByEmailAsync(email))!; id = persisted.Id;
            Assert.True(await users.CheckPasswordAsync(persisted, replacement));
            Assert.False(await users.CheckPasswordAsync(persisted, temporary));
            Assert.False(persisted.MustChangePassword);
            Assert.True(persisted.SecurityStamp != originalStamp);
        }
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/conta/perfil")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Password(client, email, replacement)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PortalFixture.Post(client, "/conta/mfa/iniciar", "/conta/mfa", new())).StatusCode);
        var secret = Regex.Match(await client.GetStringAsync("/conta/mfa"), @"<code\b[^>]*>\s*([A-Z2-7]+)\s*</code>").Groups[1].Value;
        Assert.True(secret.Length > 0);
        var recovery = await PortalFixture.Post(client, "/conta/mfa", "/conta/mfa", new() { ["code"] = Totp.CurrentCode(secret, DateTime.UtcNow) });
        Assert.Equal(HttpStatusCode.OK, recovery.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/admin")).StatusCode);
        await CompleteMfa(client, jar, app, email, replacement, secret);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin")).StatusCode);
        var ticket = Ticket(app, jar);
        Assert.True(ticket.Principal.HasClaim("amr", "mfa"));
        Assert.True(ticket.Principal.FindAll("mb:session").Count()==1);
        Assert.Equal(HttpStatusCode.Redirect, (await PortalFixture.Post(client, "/conta/sair", "/conta/perfil", new())).StatusCode);
        await CompleteMfa(client, jar, app, email, replacement, secret);
        var session = Ticket(app, jar).Principal.FindFirstValue("mb:session")!;
        using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IAdministrationRepository>().RevokeSessionAsync(session, id, default);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/admin")).StatusCode);
        await CompleteMfa(client, jar, app, email, replacement, secret);
        // New host/service provider with the same durable key ring and SQL database.
        await app.DisposeAsync();
        await using var restarted = new PortalFactory(fixture.ConnectionString, keysPath);
        using var afterRestart = Client(restarted, jar);
        Assert.Equal(HttpStatusCode.OK, (await afterRestart.GetAsync("/admin")).StatusCode);
        await PortalFixture.Post(afterRestart, "/conta/sair", "/conta/perfil", new());
        await CompleteMfa(afterRestart, jar, restarted, email, replacement, secret);
        Assert.Equal(HttpStatusCode.OK, (await afterRestart.GetAsync("/admin")).StatusCode);
    }

    [MySqlFact]
    public async Task PasswordAndTotpFailuresLockOutAndRecoveryCodesAreSingleUseOverHttp()
    {
        var account = await ConfiguredAccount();
        using var client = shared.Client();
        for (var attempt=0;attempt<5;attempt++) await Password(client,account.Email,"Wrong-password-Aa1!");
        using (var scope=shared.Factory.Services.CreateScope())
        {
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var locked=(await users.FindByEmailAsync(account.Email))!;
            Assert.True(await users.IsLockedOutAsync(locked));
            Assert.True((await users.SetLockoutEndDateAsync(locked,DateTimeOffset.UtcNow.AddMinutes(-1))).Succeeded);
        }
        await Password(client,account.Email,account.Password);
        for (var attempt=0;attempt<5;attempt++) await PortalFixture.Post(client,"/conta/verificar","/conta/verificar",new(){["code"]="invalid"});
        using (var scope=shared.Factory.Services.CreateScope())
        {
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var locked=(await users.FindByEmailAsync(account.Email))!;
            Assert.True(await users.IsLockedOutAsync(locked));
            Assert.True((await users.SetLockoutEndDateAsync(locked,DateTimeOffset.UtcNow.AddMinutes(-1))).Succeeded);
        }
        // Separate test client/IP keeps the HTTP limiter enabled without conflating it with Identity lockout.
        using var recoveryClient=shared.Client();
        await Password(recoveryClient,account.Email,account.Password);
        Assert.Equal(HttpStatusCode.Redirect,(await PortalFixture.Post(recoveryClient,"/conta/verificar","/conta/verificar",new(){["code"]=account.Recovery,["recovery"]="true"})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await recoveryClient.GetAsync("/admin")).StatusCode);
        await PortalFixture.Post(recoveryClient,"/conta/sair","/conta/perfil",new());
        await Password(recoveryClient,account.Email,account.Password);
        Assert.Equal(HttpStatusCode.OK,(await PortalFixture.Post(recoveryClient,"/conta/verificar","/conta/verificar",new(){["code"]=account.Recovery,["recovery"]="true"})).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await recoveryClient.GetAsync("/admin")).StatusCode);
    }

    [MySqlFact]
    public async Task PasswordResetInvalidatesPendingMfaCookieAndExpiredChallengesCannotAuthenticate()
    {
        var account=await ConfiguredAccount();var jar=new Dictionary<string,string>();using var client=Client(shared.Factory,jar);
        await Password(client,account.Email,account.Password);
        var options=shared.Factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.TwoFactorUserIdScheme);
        var pending=options.TicketDataFormat.Unprotect(jar[options.Cookie.Name!])!;
        var identity=(ClaimsIdentity)pending.Principal.Identity!;
        identity.RemoveClaim(identity.FindFirst(MinhaBelem.Web.Security.PortalTwoFactorCookieEvents.StampClaim)!);
        jar[options.Cookie.Name!]=options.TicketDataFormat.Protect(pending);
        Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync("/conta/verificar")).StatusCode);
        await Password(client,account.Email,account.Password);
        pending=options.TicketDataFormat.Unprotect(jar[options.Cookie.Name!])!;
        pending.Properties.ExpiresUtc=DateTimeOffset.UtcNow.AddSeconds(-5);jar[options.Cookie.Name!]=options.TicketDataFormat.Protect(pending);
        Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync("/conta/verificar")).StatusCode);
        await Password(client,account.Email,account.Password);
        using(var scope=shared.Factory.Services.CreateScope())
        {
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var user=(await users.FindByEmailAsync(account.Email))!;
            var token=await users.GeneratePasswordResetTokenAsync(user);
            Assert.True((await users.ResetPasswordAsync(user,token,"New-Aa1!"+Guid.NewGuid().ToString("N"))).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync("/conta/verificar")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync("/admin")).StatusCode);
    }

    private async Task<(string Email,string Password,string Recovery)> ConfiguredAccount()
    {
        using var scope=shared.Factory.Services.CreateScope();var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email=Guid.NewGuid()+"@example.com";var password="Aa1!"+Guid.NewGuid().ToString("N");
        var user=new ApplicationUser { Email=email,UserName=email,Enabled=true,EmailConfirmed=true,LockoutEnabled=true };
        Assert.True((await users.CreateAsync(user,password)).Succeeded);Assert.True((await users.AddToRoleAsync(user,Roles.Editor)).Succeeded);
        Assert.True((await users.ResetAuthenticatorKeyAsync(user)).Succeeded);Assert.True((await users.SetTwoFactorEnabledAsync(user,true)).Succeeded);
        var codes=(await users.GenerateNewTwoFactorRecoveryCodesAsync(user,8))!.ToArray();return(email,password,codes[0]);
    }

    private static Task<HttpResponseMessage> Password(HttpClient client, string email, string password) => PortalFixture.Post(client, "/conta/login", "/conta/login", new() { ["Email"] = email, ["Password"] = password });
    private static async Task CompleteMfa(HttpClient client, Dictionary<string,string> jar, PortalFactory app, string email, string password, string secret)
    {
        var passwordResult = await Password(client, email, password);
        Assert.EndsWith("/conta/verificar", passwordResult.Headers.Location!.ToString());
        Assert.True(jar.ContainsKey("__Host-MinhaBelem.Mfa"));
        var mfa = await PortalFixture.Post(client, "/conta/verificar", "/conta/verificar", new() { ["code"] = Totp.CurrentCode(secret, DateTime.UtcNow) });
        Assert.Equal(HttpStatusCode.Redirect, mfa.StatusCode);
        Assert.EndsWith("/admin", mfa.Headers.Location!.ToString());
        Assert.True(Ticket(app, jar).Principal.HasClaim("amr", "mfa"));
    }
    private static Microsoft.AspNetCore.Authentication.AuthenticationTicket Ticket(PortalFactory app, Dictionary<string,string> jar)
    {
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        var value = jar[options.Cookie.Name!];
        if (value.StartsWith("chunks-")) value = string.Concat(Enumerable.Range(1,int.Parse(value[7..])).Select(n=>jar[options.Cookie.Name+"C"+n]));
        return options.TicketDataFormat.Unprotect(value)!;
    }
    private static HttpClient Client(PortalFactory app, Dictionary<string,string> jar) => app.CreateDefaultClient(new Uri("https://localhost"), new Jar(jar));
    private sealed class Jar(Dictionary<string,string> cookies) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            request.Headers.Remove("Cookie");
            request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies.Select(x=>x.Key+"="+x.Value)));
            var response = await base.SendAsync(request, ct);
            if (response.Headers.TryGetValues("Set-Cookie", out var headers)) foreach (var header in headers)
            {
                if (header.StartsWith("__Host-MinhaBelem.Editor=") || header.StartsWith("__Host-MinhaBelem.Mfa="))
                {
                    Assert.True(header.Contains("secure",StringComparison.OrdinalIgnoreCase));Assert.True(header.Contains("httponly",StringComparison.OrdinalIgnoreCase));
                    Assert.True(header.Contains("path=/",StringComparison.OrdinalIgnoreCase));
                }
                var pair = header.Split(';')[0].Split('=',2);
                if (pair.Length == 2) { if (pair[1].Length == 0) cookies.Remove(pair[0]); else cookies[pair[0]]=pair[1]; }
            }
            return response;
        }
    }
    private sealed class AsyncFixture : IAsyncDisposable
    {
        public PortalFixture Value { get; } = new();
        public async ValueTask DisposeAsync() => await Value.DisposeAsync();
    }
}
