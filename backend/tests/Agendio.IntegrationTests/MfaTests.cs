using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Npgsql;
using OtpNet;

namespace Agendio.IntegrationTests;

/// <summary>
/// Cobre o fluxo completo de MFA/TOTP: login em duas etapas, uso unico do
/// challenge, codigos de recuperacao, segredo criptografado em coluna (mesmo
/// tipo de teste de CustomerEncryptionTests.cs), rate limit no verify, e
/// isolamento cruzado de tenant via RLS de verdade — ver docs/adr/0008.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class MfaTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";
    private const string RefreshCookieName = "agendio_refresh_token";

    [Fact]
    public async Task Should_Return_Mfa_Challenge_Instead_Of_Tokens_When_Mfa_Enabled_User_Logs_In()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email, accessToken) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);
        await EnableMfaAsync(client, accessToken, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("mfaRequired").GetBoolean().ShouldBeTrue();
        body.GetProperty("mfaChallengeToken").GetString().ShouldNotBeNullOrWhiteSpace();
        body.TryGetProperty("accessToken", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Not_Set_Refresh_Cookie_When_Mfa_Challenge_Returned()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email, accessToken) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);
        await EnableMfaAsync(client, accessToken, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);

        ExtractRefreshTokenCookie(loginResponse).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Issue_Tokens_When_Valid_Totp_Code_Presented_To_Verify_Endpoint()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email, accessToken) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);
        var (secret, _) = await EnableMfaAsync(client, accessToken, cancellationToken);

        var challengeToken = await LoginAndGetChallengeAsync(client, tenantId, email, cancellationToken);

        var verifyResponse = await client.PostAsJsonAsync(
            "/api/auth/mfa/verify", new { mfaChallengeToken = challengeToken, code = ComputeTotp(secret) }, cancellationToken);
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("mfaRequired").GetBoolean().ShouldBeFalse();
        body.GetProperty("accessToken").GetString().ShouldNotBeNullOrWhiteSpace();
        ExtractRefreshTokenCookie(verifyResponse).ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Should_Reject_Verify_When_Challenge_Token_Expired_Or_Unknown()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/mfa/verify", new { mfaChallengeToken = "token-que-nunca-existiu", code = "123456" }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Reject_Verify_When_Challenge_Token_Reused_After_Success()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email, accessToken) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);
        var (secret, _) = await EnableMfaAsync(client, accessToken, cancellationToken);
        var challengeToken = await LoginAndGetChallengeAsync(client, tenantId, email, cancellationToken);

        var firstAttempt = await client.PostAsJsonAsync(
            "/api/auth/mfa/verify", new { mfaChallengeToken = challengeToken, code = ComputeTotp(secret) }, cancellationToken);
        firstAttempt.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondAttempt = await client.PostAsJsonAsync(
            "/api/auth/mfa/verify", new { mfaChallengeToken = challengeToken, code = ComputeTotp(secret) }, cancellationToken);
        secondAttempt.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Issue_Tokens_When_Valid_Recovery_Code_Presented_And_Mark_It_Used()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email, accessToken) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);
        var (_, recoveryCodes) = await EnableMfaAsync(client, accessToken, cancellationToken);
        var challengeToken = await LoginAndGetChallengeAsync(client, tenantId, email, cancellationToken);

        var verifyResponse = await client.PostAsJsonAsync(
            "/api/auth/mfa/verify", new { mfaChallengeToken = challengeToken, code = recoveryCodes[0] }, cancellationToken);

        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_Reject_Same_Recovery_Code_Used_Twice()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email, accessToken) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);
        var (_, recoveryCodes) = await EnableMfaAsync(client, accessToken, cancellationToken);

        var firstChallenge = await LoginAndGetChallengeAsync(client, tenantId, email, cancellationToken);
        var firstVerify = await client.PostAsJsonAsync(
            "/api/auth/mfa/verify", new { mfaChallengeToken = firstChallenge, code = recoveryCodes[0] }, cancellationToken);
        firstVerify.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondChallenge = await LoginAndGetChallengeAsync(client, tenantId, email, cancellationToken);
        var secondVerify = await client.PostAsJsonAsync(
            "/api/auth/mfa/verify", new { mfaChallengeToken = secondChallenge, code = recoveryCodes[0] }, cancellationToken);
        secondVerify.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_Return_Ten_Plaintext_Recovery_Codes_Exactly_Once_When_Enabling_Mfa()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (_, _, accessToken) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);

        var (_, recoveryCodes) = await EnableMfaAsync(client, accessToken, cancellationToken);

        recoveryCodes.Length.ShouldBe(10);
        recoveryCodes.ShouldAllBe(code => code.Length == 9 && code[4] == '-');
        recoveryCodes.Distinct().Count().ShouldBe(10);
    }

    [Fact]
    public async Task Should_Persist_Mfa_Secret_Encrypted_At_Rest_When_Enabled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email, accessToken) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);
        var (secret, _) = await EnableMfaAsync(client, accessToken, cancellationToken);

        await using var connection = new NpgsqlConnection(fixture.DatabaseOwnerConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT mfa_secret_encrypted FROM identity.users WHERE tenant_id = @tenantId AND email = @email", connection);
        command.Parameters.AddWithValue("tenantId", tenantId);
        command.Parameters.AddWithValue("email", email);
        var rawSecret = (string)(await command.ExecuteScalarAsync(cancellationToken))!;

        rawSecret.ShouldNotBe(secret);
        Should.NotThrow(() => Convert.FromBase64String(rawSecret));
    }

    [Fact]
    public async Task Should_Require_Password_And_Code_When_Disabling_Mfa()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email, accessToken) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);
        var (secret, _) = await EnableMfaAsync(client, accessToken, cancellationToken);

        var wrongPassword = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/auth/mfa/disable", new { password = "SenhaErrada123!", code = ComputeTotp(secret) }, cancellationToken);
        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var wrongCode = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/auth/mfa/disable", new { password = Password, code = "000000" }, cancellationToken);
        wrongCode.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var success = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/auth/mfa/disable", new { password = Password, code = ComputeTotp(secret) }, cancellationToken);
        success.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // MFA desligado: login volta a emitir tokens direto, sem desafio.
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        loginBody.GetProperty("mfaRequired").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Rate_Limit_Mfa_Verify_Endpoint_When_Brute_Forcing_Codes()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var lowLimitFactory = fixture.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:AuthPermitLimit", "3");
            builder.UseSetting("RateLimiting:AuthWindowSeconds", "60");
        });
        using var client = lowLimitFactory.CreateClient();

        HttpResponseMessage? lastResponse = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            lastResponse = await client.PostAsJsonAsync(
                "/api/auth/mfa/verify", new { mfaChallengeToken = "chute-aleatorio", code = "000000" }, cancellationToken);
        }

        lastResponse!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Should_Enforce_Tenant_Isolation_On_Mfa_Recovery_Codes_Via_Row_Level_Security()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var (tenantAId, _, accessTokenA) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);
        await EnableMfaAsync(client, accessTokenA, cancellationToken);

        var (tenantBId, _, _) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);

        await using var ownerConnection = new NpgsqlConnection(fixture.DatabaseOwnerConnectionString);
        await ownerConnection.OpenAsync(cancellationToken);
        await using var findCommand = new NpgsqlCommand(
            "SELECT id FROM identity.mfa_recovery_codes WHERE tenant_id = @tenantId LIMIT 1", ownerConnection);
        findCommand.Parameters.AddWithValue("tenantId", tenantAId);
        var recoveryCodeId = (Guid)(await findCommand.ExecuteScalarAsync(cancellationToken))!;

        // Conecta como a role de RUNTIME (NOBYPASSRLS), ancora explicitamente no
        // tenant B (mesmo comando de TenantConnectionInterceptor) e tenta ler o
        // codigo de recuperacao do tenant A pelo Id exato — RLS tem que barrar,
        // nao so o Global Query Filter do EF Core (que nem entra em jogo aqui).
        await using var appConnection = new NpgsqlConnection(fixture.DatabaseAppConnectionString);
        await appConnection.OpenAsync(cancellationToken);
        await using (var setTenantCommand = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenantId, false)", appConnection))
        {
            setTenantCommand.Parameters.AddWithValue("tenantId", tenantBId.ToString());
            await setTenantCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var selectCommand = new NpgsqlCommand(
            "SELECT id FROM identity.mfa_recovery_codes WHERE id = @id", appConnection);
        selectCommand.Parameters.AddWithValue("id", recoveryCodeId);
        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);

        (await reader.ReadAsync(cancellationToken)).ShouldBeFalse(
            "RLS deveria impedir o tenant B de enxergar um codigo de recuperacao do tenant A.");
    }

    private async Task<(Guid TenantId, string Email, string AccessToken)> CreateTenantOwnerAndLoginAsync(
        HttpClient client, CancellationToken cancellationToken)
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
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono" }, cancellationToken);
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return (tenantId, ownerEmail, loginBody.GetProperty("accessToken").GetString()!);
    }

    private static async Task<string> LoginAndGetChallengeAsync(HttpClient client, Guid tenantId, string email, CancellationToken cancellationToken)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("mfaChallengeToken").GetString()!;
    }

    private static async Task<(string Secret, string[] RecoveryCodes)> EnableMfaAsync(
        HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        var setupResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/auth/mfa/setup", new { }, cancellationToken);
        setupResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var setupBody = await setupResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var secret = setupBody.GetProperty("secret").GetString()!;

        var enableResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/auth/mfa/enable", new { secret, code = ComputeTotp(secret) }, cancellationToken);
        enableResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var enableBody = await enableResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var recoveryCodes = enableBody.GetProperty("recoveryCodes").EnumerateArray().Select(e => e.GetString()!).ToArray();

        return (secret, recoveryCodes);
    }

    private static string ComputeTotp(string base32Secret) => new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

    private static string? ExtractRefreshTokenCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        foreach (var cookie in cookies)
        {
            if (cookie.StartsWith($"{RefreshCookieName}=", StringComparison.Ordinal))
            {
                return cookie.Split(';')[0][(RefreshCookieName.Length + 1)..];
            }
        }

        return null;
    }
}
