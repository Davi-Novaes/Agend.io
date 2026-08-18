using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// Prova o requisito de seguranca de rotacao de refresh token com deteccao de
/// reuso: usar um token JA trocado (sinal classico de roubo/replay) revoga a
/// familia inteira, nao so aquele token — forcando novo login em todos os
/// dispositivos daquela sessao.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class RefreshTokenFlowTests(IntegrationTestFixture fixture)
{
    private const string RefreshCookieName = "agendio_refresh_token";
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Refreshing_With_A_Valid_Token_Should_Issue_A_New_Rotated_Token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email) = await RegisterAndCreateTenantAsync(client, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var firstRefreshToken = ExtractRefreshTokenCookie(loginResponse);
        firstRefreshToken.ShouldNotBeNullOrEmpty();

        var refreshResponse = await SendRefreshAsync(client, firstRefreshToken!, cancellationToken);
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondRefreshToken = ExtractRefreshTokenCookie(refreshResponse);
        secondRefreshToken.ShouldNotBeNullOrEmpty();
        secondRefreshToken.ShouldNotBe(firstRefreshToken);
    }

    [Fact]
    public async Task Reusing_An_Already_Rotated_Refresh_Token_Should_Revoke_The_Entire_Family()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email) = await RegisterAndCreateTenantAsync(client, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        var firstToken = ExtractRefreshTokenCookie(loginResponse)!;

        // Rotacao legitima: troca o token original por um novo.
        var firstRotation = await SendRefreshAsync(client, firstToken, cancellationToken);
        firstRotation.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondToken = ExtractRefreshTokenCookie(firstRotation)!;

        // Reuso do token JA TROCADO — o handler deve tratar isso como sinal de
        // roubo e recusar.
        var reuseAttempt = await SendRefreshAsync(client, firstToken, cancellationToken);
        reuseAttempt.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // E revogar a FAMILIA INTEIRA: o token novo (que ate agora era valido)
        // tambem para de funcionar, forcando login de novo em todo dispositivo
        // daquela sessao.
        var secondTokenAfterReuse = await SendRefreshAsync(client, secondToken, cancellationToken);
        secondTokenAfterReuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_And_Refresh_Should_Both_Include_FullName_Claim_In_Access_Token()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email) = await RegisterAndCreateTenantAsync(client, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        ReadClaim(loginBody.GetProperty("accessToken").GetString()!, "full_name").ShouldBe("Usuario de Teste");

        var refreshToken = ExtractRefreshTokenCookie(loginResponse)!;
        var refreshResponse = await SendRefreshAsync(client, refreshToken, cancellationToken);
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        ReadClaim(refreshBody.GetProperty("accessToken").GetString()!, "full_name").ShouldBe("Usuario de Teste");
    }

    [Fact]
    public async Task Refreshing_Without_A_Cookie_Should_Return_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.PostAsync("/api/auth/refresh", content: null, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<(Guid TenantId, string Email)> RegisterAndCreateTenantAsync(HttpClient client, CancellationToken cancellationToken)
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

        var email = $"user-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email, password = Password, fullName = "Usuario de Teste" }, cancellationToken);
        registerResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        await fixture.ConfirmEmailDirectlyAsync(tenantId, email, cancellationToken);

        return (tenantId, email);
    }

    private static Task<HttpResponseMessage> SendRefreshAsync(HttpClient client, string refreshToken, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshToken}");
        return client.SendAsync(request, cancellationToken);
    }

    private static string ReadClaim(string accessToken, string claimType)
    {
        var payloadSegment = accessToken.Split('.')[1];
        var padded = payloadSegment.PadRight(payloadSegment.Length + ((4 - (payloadSegment.Length % 4)) % 4), '=');
        var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/')));
        var claims = JsonSerializer.Deserialize<JsonElement>(payloadJson);
        return claims.GetProperty(claimType).GetString()!;
    }

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
