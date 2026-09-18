using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// Troca de senha autenticada (usuario logado, sabe a senha atual) — distinta
/// de PasswordResetTests (token por e-mail, presume senha esquecida). Mesmo
/// efeito colateral de seguranca: revoga todas as sessoes ativas ao concluir.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class ChangePasswordTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";
    private const string NewPassword = "OutraSenhaForte456!";
    private const string RefreshCookieName = "agendio_refresh_token";

    [Fact]
    public async Task Changing_Password_With_Correct_Current_Password_Should_Allow_Login_With_The_New_One()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email, accessToken) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);

        var changeResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/auth/change-password", new { currentPassword = Password, newPassword = NewPassword }, cancellationToken);
        changeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var oldPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        oldPasswordLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var newPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = NewPassword }, cancellationToken);
        newPasswordLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Changing_Password_With_Wrong_Current_Password_Should_Be_Rejected_And_Leave_The_Old_Password_Working()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email, accessToken) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);

        var changeResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/auth/change-password", new { currentPassword = "SenhaErrada123!", newPassword = NewPassword }, cancellationToken);
        changeResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var oldPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        oldPasswordLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Changing_Password_Without_Authentication_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/change-password", new { currentPassword = Password, newPassword = NewPassword }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Changing_Password_Should_Revoke_All_Active_Refresh_Tokens()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email, accessToken) = await CreateTenantOwnerAndLoginAsync(client, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        var refreshToken = ExtractRefreshTokenCookie(loginResponse)!;

        var changeResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/auth/change-password", new { currentPassword = Password, newPassword = NewPassword }, cancellationToken);
        changeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var refreshAfterChange = await SendRefreshAsync(client, refreshToken, cancellationToken);
        refreshAfterChange.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono", phone = "+5511999999999", cpfCnpj = "12345678909", termsAccepted = true }, cancellationToken);
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return (tenantId, ownerEmail, loginBody.GetProperty("accessToken").GetString()!);
    }

    private static Task<HttpResponseMessage> SendRefreshAsync(HttpClient client, string refreshToken, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshToken}");
        return client.SendAsync(request, cancellationToken);
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
