using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agendio.IntegrationTests;

/// <summary>
/// Cobre o fluxo real de "esqueci minha senha" ponta a ponta via MailHog (mesmo
/// padrao de EmailConfirmationTests): sempre 204 (evita enumeracao de conta),
/// token de uso unico e expiravel, e revogacao de TODAS as sessoes ativas ao
/// concluir a redefinicao — ver ResetPasswordCommandHandler.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class PasswordResetTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";
    private const string NewPassword = "OutraSenhaForte456!";
    private const string RefreshCookieName = "agendio_refresh_token";

    [Fact]
    public async Task Forgot_Password_For_An_Unknown_Email_Should_Still_Return_NoContent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, _) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password", new { tenantId, email = $"nao-existe-{Guid.NewGuid():N}@example.com" }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Resetting_Password_Via_The_Real_Link_Received_By_Email_Should_Allow_Login_With_The_New_Password()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var forgotResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new { tenantId, email }, cancellationToken);
        forgotResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var token = await ExtractLatestResetTokenFromMailHogAsync(email, 1, cancellationToken);

        var resetResponse = await client.PostAsJsonAsync(
            "/api/auth/reset-password", new { token, newPassword = NewPassword }, cancellationToken);
        resetResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var oldPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        oldPasswordLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var newPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = NewPassword }, cancellationToken);
        newPasswordLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resetting_With_An_Unknown_Token_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password", new { token = "token-que-nunca-existiu", newPassword = NewPassword }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reusing_The_Same_Reset_Token_Twice_Should_Reject_The_Second_Attempt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email) = await CreateTenantWithOwnerAsync(client, cancellationToken);
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { tenantId, email }, cancellationToken);
        var token = await ExtractLatestResetTokenFromMailHogAsync(email, 1, cancellationToken);

        var firstReset = await client.PostAsJsonAsync(
            "/api/auth/reset-password", new { token, newPassword = NewPassword }, cancellationToken);
        firstReset.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var secondReset = await client.PostAsJsonAsync(
            "/api/auth/reset-password", new { token, newPassword = "MaisUmaSenhaForte789!" }, cancellationToken);
        secondReset.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Resetting_Password_Should_Revoke_All_Active_Refresh_Tokens()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        var refreshToken = ExtractRefreshTokenCookie(loginResponse)!;

        await client.PostAsJsonAsync("/api/auth/forgot-password", new { tenantId, email }, cancellationToken);
        var token = await ExtractLatestResetTokenFromMailHogAsync(email, 1, cancellationToken);
        var resetResponse = await client.PostAsJsonAsync(
            "/api/auth/reset-password", new { token, newPassword = NewPassword }, cancellationToken);
        resetResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // A sessao que existia ANTES da redefinicao precisa parar de funcionar —
        // quem redefiniu por nao lembrar a senha quer derrubar qualquer sessao
        // que ainda esteja usando a senha antiga (ver ResetPasswordCommandHandler).
        var refreshAfterReset = await SendRefreshAsync(client, refreshToken, cancellationToken);
        refreshAfterReset.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

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

    /// <summary>Mesma tecnica de ExtractLatestConfirmationTokenFromMailHogAsync (EmailConfirmationTests), so troca o path esperado no link.</summary>
    private async Task<string> ExtractLatestResetTokenFromMailHogAsync(string email, int expectedCount, CancellationToken cancellationToken)
    {
        using var mailHogClient = new HttpClient { BaseAddress = new Uri(fixture.MailHogApiBaseUrl) };

        for (var attempt = 0; attempt < 30; attempt++)
        {
            var messagesJson = await mailHogClient.GetStringAsync("/api/v2/messages", cancellationToken);
            var document = JsonDocument.Parse(messagesJson);

            var matching = document.RootElement.GetProperty("items").EnumerateArray()
                .Where(item => item.GetProperty("Content").GetProperty("Headers").GetProperty("To")
                    .EnumerateArray().Any(to => to.GetString()!.Contains(email, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(item => item.GetProperty("Created").GetString(), StringComparer.Ordinal)
                .ToList();

            if (matching.Count >= expectedCount)
            {
                var html = DecodeQuotedPrintable(matching[expectedCount - 1].GetProperty("Content").GetProperty("Body").GetString()!);
                var token = ExtractTokenFromHtml(html);
                if (token is not null)
                {
                    return token;
                }
            }

            await Task.Delay(200, cancellationToken);
        }

        throw new InvalidOperationException(
            $"Nao chegou um {expectedCount}o e-mail de redefinicao de senha valido para {email} no MailHog a tempo.");
    }

    private static string? ExtractTokenFromHtml(string html)
    {
        var match = Regex.Match(html, "/reset-password/([A-Za-z0-9_-]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string DecodeQuotedPrintable(string input)
    {
        var withoutSoftBreaks = input.Replace("=\r\n", string.Empty).Replace("=\n", string.Empty);
        return Regex.Replace(withoutSoftBreaks, "=([0-9A-Fa-f]{2})", static m =>
            ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
    }
}
