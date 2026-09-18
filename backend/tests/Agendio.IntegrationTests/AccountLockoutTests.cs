using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Platform.Domain;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OtpNet;

namespace Agendio.IntegrationTests;

/// <summary>
/// Cobre bloqueio de conta por tentativas de login incorretas (Identity e
/// Platform) e o log de auditoria de seguranca gravado em cada tentativa — ver
/// LoginCommandHandler/LoginPlatformAdminCommandHandler e SecurityAuditLogger.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class AccountLockoutTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";
    private const string AdminPassword = "SenhaDoAdmin123!";

    [Fact]
    public async Task Five_Wrong_Password_Attempts_Should_Lock_The_Account()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/login", new { tenantId, email, password = "SenhaErrada!" }, cancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // Conta trancada: mesmo a senha CORRETA continua rejeitando, mas com um
        // codigo DISTINTO do generico (Auth.AccountLocked, com lockedUntilUtc) —
        // pedido de produto pra mostrar quanto tempo falta em vez de deixar a
        // pessoa martelando sem saber o motivo (ver LoginCommandHandler.AccountLockedError).
        var correctPasswordWhileLocked = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        correctPasswordWhileLocked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await correctPasswordWhileLocked.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("title").GetString().ShouldBe("Auth.AccountLocked");
        body.GetProperty("lockedUntilUtc").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Fewer_Than_Five_Wrong_Attempts_Should_Not_Lock_The_Account()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new { tenantId, email, password = "SenhaErrada!" }, cancellationToken);
        }

        var correctPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        correctPasswordLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_Successful_Login_Should_Reset_The_Failed_Attempt_Counter()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        for (var attempt = 0; attempt < 4; attempt++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new { tenantId, email, password = "SenhaErrada!" }, cancellationToken);
        }

        var successfulLogin = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        successfulLogin.StatusCode.ShouldBe(HttpStatusCode.OK);

        // O contador zerou: mais 4 tentativas erradas (que sozinhas nao
        // travariam) nao devem travar a conta agora.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new { tenantId, email, password = "SenhaErrada!" }, cancellationToken);
        }

        var loginAfter = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        loginAfter.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Failed_And_Successful_Logins_Should_Be_Recorded_In_The_Security_Audit_Log()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        await client.PostAsJsonAsync("/api/auth/login", new { tenantId, email, password = "SenhaErrada!" }, cancellationToken);
        await client.PostAsJsonAsync("/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);

        await using var connection = new NpgsqlConnection(fixture.DatabaseOwnerConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT event_type, success FROM identity.security_audit_log WHERE tenant_id = @tenantId ORDER BY occurred_at_utc", connection);
        command.Parameters.AddWithValue("tenantId", tenantId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var events = new List<(string EventType, bool Success)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add((reader.GetString(0), reader.GetBoolean(1)));
        }

        events.ShouldContain(e => e.EventType == "LoginFailed" && !e.Success);
        events.ShouldContain(e => e.EventType == "LoginSucceeded" && e.Success);
    }

    [Fact]
    public async Task Platform_Admin_Five_Wrong_Password_Attempts_Should_Lock_The_Account()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var email = await SeedPlatformAdminAsync(cancellationToken);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await client.PostAsJsonAsync("/api/platform/auth/login", new { email, password = "SenhaErrada!" }, cancellationToken);
        }

        var correctPasswordWhileLocked = await client.PostAsJsonAsync(
            "/api/platform/auth/login", new { email, password = AdminPassword }, cancellationToken);
        correctPasswordWhileLocked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Platform_Admin_Can_Setup_Enable_And_Login_With_Mfa()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var email = await SeedPlatformAdminAsync(cancellationToken);

        var loginResponse = await client.PostAsJsonAsync("/api/platform/auth/login", new { email, password = AdminPassword }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var accessToken = loginBody.GetProperty("accessToken").GetString()!;

        var setupResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/platform/auth/mfa/setup", new { }, cancellationToken);
        setupResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var setupBody = await setupResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var secret = setupBody.GetProperty("secret").GetString()!;

        var enableResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/platform/auth/mfa/enable", new { secret, code = ComputeTotp(secret) }, cancellationToken);
        enableResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Login volta a exigir o segundo fator a partir de agora.
        var secondLoginResponse = await client.PostAsJsonAsync(
            "/api/platform/auth/login", new { email, password = AdminPassword }, cancellationToken);
        var secondLoginBody = await secondLoginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        secondLoginBody.GetProperty("mfaRequired").GetBoolean().ShouldBeTrue();
        var challengeToken = secondLoginBody.GetProperty("mfaChallengeToken").GetString()!;

        var verifyResponse = await client.PostAsJsonAsync(
            "/api/platform/auth/mfa/verify", new { mfaChallengeToken = challengeToken, code = ComputeTotp(secret) }, cancellationToken);
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        verifyBody.GetProperty("accessToken").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    private static string ComputeTotp(string base32Secret) => new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

    private async Task<(Guid TenantId, string OwnerEmail)> CreateTenantWithOwnerAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var tenantResponse = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"Tenant {Guid.NewGuid():N}",
            slug = $"tenant-{Guid.NewGuid():N}",
            businessType = "Other",
            timeZoneId = "America/Sao_Paulo",
        }, cancellationToken);
        tenantResponse.EnsureSuccessStatusCode();
        var tenantBody = await tenantResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var tenantId = tenantBody.GetProperty("id").GetGuid();

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono", phone = "+5511999999999", cpfCnpj = "12345678909", termsAccepted = true }, cancellationToken);
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        return (tenantId, ownerEmail);
    }

    private async Task<string> SeedPlatformAdminAsync(CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var email = $"admin-{Guid.NewGuid():N}@example.com";
        var admin = PlatformAdmin.Create(email, "Admin de Teste", passwordHasher.Hash(AdminPassword)).Value;
        dbContext.PlatformAdmins.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);

        return email;
    }
}
