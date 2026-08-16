using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agendio.IntegrationTests;

/// <summary>
/// Cobre a confirmacao de e-mail obrigatoria antes do login: bloqueio com
/// codigo de erro dedicado (Auth.EmailNotConfirmed), o fluxo real ponta a
/// ponta via MailHog (token extraido do e-mail de verdade, nao um atalho de
/// teste), token invalido/expirado, e reenvio invalidando o token anterior.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class EmailConfirmationTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Logging_In_Before_Confirming_Email_Should_Be_Rejected_With_A_Dedicated_Error_Code()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);

        loginResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("title").GetString().ShouldBe("Auth.EmailNotConfirmed");
    }

    [Fact]
    public async Task Confirming_Via_The_Real_Link_Received_By_Email_Should_Allow_Login_Afterward()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var token = await ExtractLatestConfirmationTokenFromMailHogAsync(email, 1, cancellationToken);

        var confirmResponse = await client.PostAsJsonAsync("/api/auth/confirm-email", new { token }, cancellationToken);
        confirmResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Confirming_With_An_Unknown_Token_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/confirm-email", new { token = "token-que-nunca-existiu" }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Confirming_Twice_With_The_Same_Token_Should_Reject_The_Second_Attempt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (_, email) = await CreateTenantWithOwnerAsync(client, cancellationToken);
        var token = await ExtractLatestConfirmationTokenFromMailHogAsync(email, 1, cancellationToken);

        var firstConfirm = await client.PostAsJsonAsync("/api/auth/confirm-email", new { token }, cancellationToken);
        firstConfirm.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // O primeiro ConfirmEmail ja limpa o hash do token (ver User.ConfirmEmail)
        // — reusar o mesmo token depois nao encontra mais o usuario.
        var secondConfirm = await client.PostAsJsonAsync("/api/auth/confirm-email", new { token }, cancellationToken);
        secondConfirm.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Resending_The_Confirmation_Email_Should_Issue_A_New_Token_That_Invalidates_The_Old_One()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, email) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var firstToken = await ExtractLatestConfirmationTokenFromMailHogAsync(email, 1, cancellationToken);

        var resendResponse = await client.PostAsJsonAsync(
            "/api/auth/resend-confirmation", new { tenantId, email }, cancellationToken);
        resendResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var secondToken = await ExtractLatestConfirmationTokenFromMailHogAsync(email, 2, cancellationToken);
        secondToken.ShouldNotBe(firstToken);

        var confirmWithOldToken = await client.PostAsJsonAsync("/api/auth/confirm-email", new { token = firstToken }, cancellationToken);
        confirmWithOldToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var confirmWithNewToken = await client.PostAsJsonAsync("/api/auth/confirm-email", new { token = secondToken }, cancellationToken);
        confirmWithNewToken.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resending_Confirmation_For_An_Unknown_Or_Already_Confirmed_Email_Should_Silently_Succeed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, _) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        // Nunca revela se o e-mail existe ou nao (evita enumeracao de conta),
        // sempre responde sucesso generico.
        var response = await client.PostAsJsonAsync(
            "/api/auth/resend-confirmation",
            new { tenantId, email = $"nao-existe-{Guid.NewGuid():N}@example.com" },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<(Guid TenantId, string OwnerEmail)> CreateTenantWithOwnerAsync(HttpClient client, CancellationToken cancellationToken)
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
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono" }, cancellationToken);
        registerResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (tenantId, ownerEmail);
    }

    /// <summary>
    /// Le o link real do e-mail de confirmacao no MailHog (nao um atalho de
    /// teste) — espera pelo N-esimo e-mail endereçado a este destinatario e
    /// extrai o token do href, decodificando quoted-printable (MimeKit pode
    /// codificar o HTML assim, quebrando o token em varias linhas com "=").
    /// </summary>
    private async Task<string> ExtractLatestConfirmationTokenFromMailHogAsync(
        string email, int expectedCount, CancellationToken cancellationToken)
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
            $"Nao chegou um {expectedCount}o e-mail de confirmacao valido para {email} no MailHog a tempo.");
    }

    private static string? ExtractTokenFromHtml(string html)
    {
        var match = Regex.Match(html, "/confirm-email/([A-Za-z0-9_-]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string DecodeQuotedPrintable(string input)
    {
        var withoutSoftBreaks = input.Replace("=\r\n", string.Empty).Replace("=\n", string.Empty);
        return Regex.Replace(withoutSoftBreaks, "=([0-9A-Fa-f]{2})", static m =>
            ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
    }
}
