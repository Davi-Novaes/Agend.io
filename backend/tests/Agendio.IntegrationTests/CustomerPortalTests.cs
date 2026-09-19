using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agendio.IntegrationTests;

[Collection(nameof(IntegrationTestCollection))]
public class CustomerPortalTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Customer_Should_Access_Only_Their_Portal_With_The_One_Time_Code_Received_By_Email()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (resourceId, serviceId) = await CreateResourceAndServiceAsync(client, accessToken, cancellationToken);
        var customerEmail = $"portal-{Guid.NewGuid():N}@example.com";

        var bookingResponse = await client.PostAsJsonAsync($"/api/public/tenants/{tenantId}/appointments", new
        {
            resourceId,
            serviceId,
            startAtUtc = DateTimeOffset.UtcNow.AddDays(10),
            customerFullName = "Cliente do Portal",
            customerEmail,
            customerPhone = "+5511999999999",
            notes = (string?)null,
        }, cancellationToken);
        bookingResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var requestCodeResponse = await client.PostAsJsonAsync(
            $"/api/public/tenants/{tenantId}/customer-portal/request-code", new { email = customerEmail }, cancellationToken);
        requestCodeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var code = await ExtractLatestPortalCodeFromMailHogAsync(customerEmail, cancellationToken);
        var verifyResponse = await client.PostAsJsonAsync(
            $"/api/public/tenants/{tenantId}/customer-portal/verify-code", new { email = customerEmail, code }, cancellationToken);
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var portalResponse = await client.GetAsync(
            $"/api/public/tenants/{tenantId}/customer-portal/me", cancellationToken);
        portalResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var portal = await portalResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        portal.GetProperty("fullName").GetString().ShouldBe("Cliente do Portal");
        portal.GetProperty("email").GetString().ShouldBe(customerEmail);
        var appointments = portal.GetProperty("appointments").EnumerateArray().ToList();
        appointments.Count.ShouldBe(1);
        appointments[0].GetProperty("serviceName").GetString().ShouldBe("Servico do Portal");
        appointments[0].GetProperty("resourceName").GetString().ShouldBe("Profissional do Portal");
    }

    [Fact]
    public async Task Customer_Should_Be_Able_To_Register_Without_A_Prior_Booking_And_Log_In()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, _) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var customerEmail = $"self-registered-{Guid.NewGuid():N}@example.com";

        var registerResponse = await client.PostAsJsonAsync($"/api/public/tenants/{tenantId}/customer-portal/register", new
        {
            fullName = "Cliente Auto Cadastro",
            email = customerEmail,
            phone = "+5511999999999",
        }, cancellationToken);
        registerResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var code = await ExtractLatestPortalCodeFromMailHogAsync(customerEmail, cancellationToken);
        var verifyResponse = await client.PostAsJsonAsync(
            $"/api/public/tenants/{tenantId}/customer-portal/verify-code", new { email = customerEmail, code }, cancellationToken);
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var portalResponse = await client.GetAsync($"/api/public/tenants/{tenantId}/customer-portal/me", cancellationToken);
        portalResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var portal = await portalResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        portal.GetProperty("fullName").GetString().ShouldBe("Cliente Auto Cadastro");
        portal.GetProperty("email").GetString().ShouldBe(customerEmail);
    }

    [Fact]
    public async Task Customer_Portal_Registration_Should_Not_Leak_Customers_Across_Tenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantAId, _) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (tenantBId, _) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var sharedEmail = $"mesmo-email-{Guid.NewGuid():N}@example.com";

        // Tenant A e completado por inteiro antes do Tenant B comecar --
        // evita corrida no MailHog (mesmo e-mail, "ultima mensagem" ficaria
        // ambigua entre os dois jobs assincronos se disparados juntos).
        await client.PostAsJsonAsync($"/api/public/tenants/{tenantAId}/customer-portal/register", new
        {
            fullName = "Cliente do Tenant A",
            email = sharedEmail,
            phone = (string?)null,
        }, cancellationToken);
        var codeA = await ExtractLatestPortalCodeFromMailHogAsync(sharedEmail, cancellationToken);
        (await client.PostAsJsonAsync(
            $"/api/public/tenants/{tenantAId}/customer-portal/verify-code", new { email = sharedEmail, code = codeA }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        await client.PostAsJsonAsync($"/api/public/tenants/{tenantBId}/customer-portal/register", new
        {
            fullName = "Cliente do Tenant B",
            email = sharedEmail,
            phone = (string?)null,
        }, cancellationToken);

        var codeB = await ExtractLatestPortalCodeFromMailHogAsync(sharedEmail, cancellationToken, excludeCode: codeA);
        var verifyResponse = await client.PostAsJsonAsync(
            $"/api/public/tenants/{tenantBId}/customer-portal/verify-code", new { email = sharedEmail, code = codeB }, cancellationToken);
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var portalResponse = await client.GetAsync($"/api/public/tenants/{tenantBId}/customer-portal/me", cancellationToken);
        var portal = await portalResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        portal.GetProperty("fullName").GetString().ShouldBe("Cliente do Tenant B");
    }

    [Fact]
    public async Task Customer_Portal_Session_Should_Not_Cross_Tenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantAId, tenantAToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var customerEmail = $"isolado-{Guid.NewGuid():N}@example.com";

        var createCustomerResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantAToken, "/api/customers",
            new { fullName = "Cliente Isolado", email = customerEmail, phone = (string?)null, notes = (string?)null, dateOfBirth = (string?)null, customData = (object?)null },
            cancellationToken);
        createCustomerResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        await client.PostAsJsonAsync(
            $"/api/public/tenants/{tenantAId}/customer-portal/request-code", new { email = customerEmail }, cancellationToken);
        var code = await ExtractLatestPortalCodeFromMailHogAsync(customerEmail, cancellationToken);
        var verifyResponse = await client.PostAsJsonAsync(
            $"/api/public/tenants/{tenantAId}/customer-portal/verify-code", new { email = customerEmail, code }, cancellationToken);
        var setCookie = verifyResponse.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("agendio_customer_session=", StringComparison.Ordinal));
        var cookiePair = setCookie.Split(';')[0];

        var (tenantBId, _) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        using var crossTenantRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/public/tenants/{tenantBId}/customer-portal/me");
        crossTenantRequest.Headers.Add("Cookie", cookiePair);
        var crossTenantResponse = await client.SendAsync(crossTenantRequest, cancellationToken);

        crossTenantResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// excludeCode ignora um codigo ja conhecido (ex.: de um envio anterior
    /// para o MESMO e-mail em outro tenant) — sem isso, a "ultima mensagem"
    /// encontrada logo no primeiro poll seria a antiga, antes do novo job do
    /// Hangfire sequer ter rodado, e o teste pegaria o codigo errado em vez
    /// de esperar o novo chegar.
    /// </summary>
    private async Task<string> ExtractLatestPortalCodeFromMailHogAsync(
        string email, CancellationToken cancellationToken, string? excludeCode = null)
    {
        using var mailHogClient = new HttpClient { BaseAddress = new Uri(fixture.MailHogApiBaseUrl) };

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var document = JsonDocument.Parse(await mailHogClient.GetStringAsync("/api/v2/messages", cancellationToken));
            var messages = document.RootElement.GetProperty("items").EnumerateArray()
                .Where(item => item.GetProperty("Content").GetProperty("Headers").GetProperty("To")
                    .EnumerateArray().Any(to => to.GetString()!.Contains(email, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(item => item.GetProperty("Created").GetString(), StringComparer.Ordinal);

            foreach (var message in messages)
            {
                var body = DecodeQuotedPrintable(message.GetProperty("Content").GetProperty("Body").GetString()!);
                var match = Regex.Match(body, @">(\d{6})<");
                if (match.Success && match.Groups[1].Value != excludeCode)
                {
                    return match.Groups[1].Value;
                }
            }

            await Task.Delay(200, cancellationToken);
        }

        throw new InvalidOperationException($"O código do portal não chegou para {email} no MailHog a tempo.");
    }

    private static string DecodeQuotedPrintable(string input)
    {
        var withoutSoftBreaks = input.Replace("=\r\n", string.Empty).Replace("=\n", string.Empty);
        return Regex.Replace(withoutSoftBreaks, "=([0-9A-Fa-f]{2})", static match =>
            ((char)Convert.ToInt32(match.Groups[1].Value, 16)).ToString());
    }

    private static async Task<(Guid ResourceId, Guid ServiceId)> CreateResourceAndServiceAsync(
        HttpClient client, string accessToken, CancellationToken cancellationToken)
    {
        var resourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Profissional do Portal", type = "Person", capacity = 1, description = (string?)null }, cancellationToken);
        var resourceId = (await resourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        var serviceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/services",
            new { name = "Servico do Portal", description = (string?)null, durationMinutes = 30, price = 50m, currency = "BRL", category = (string?)null }, cancellationToken);
        var serviceId = (await serviceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        return (resourceId, serviceId);
    }

    private async Task<(Guid TenantId, string AccessToken)> CreateTenantWithOwnerAndLoginAsync(
        HttpClient client, CancellationToken cancellationToken)
    {
        var tenantResponse = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"Tenant {Guid.NewGuid():N}",
            slug = $"tenant-{Guid.NewGuid():N}",
            businessType = "Other",
            timeZoneId = "America/Sao_Paulo",
        }, cancellationToken);
        var tenantId = (await tenantResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            tenantId,
            email = ownerEmail,
            password = Password,
            fullName = "Dono",
            phone = "+5511999999999",
            cpfCnpj = "12345678909",
            termsAccepted = true,
        }, cancellationToken);
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var login = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return (tenantId, login.GetProperty("accessToken").GetString()!);
    }
}
