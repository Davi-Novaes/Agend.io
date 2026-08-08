using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// Campanha manual de e-mail: o envio e sincrono do ponto de vista do dominio
/// (Campaign nasce ja com RecipientCount correto), mas a entrega de cada
/// e-mail e assincrona via Hangfire — por isso o teste de entrega real usa o
/// mesmo padrao de polling no MailHog que AppointmentNotificationTests.cs.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class MarketingTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Sending_A_Campaign_Should_Only_Count_Active_Customers_With_Email()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        // 3 ativos com e-mail — contam.
        await CreateCustomerAsync(client, accessToken, "Cliente Ativo 1", $"ativo1-{Guid.NewGuid():N}@example.com", cancellationToken);
        await CreateCustomerAsync(client, accessToken, "Cliente Ativo 2", $"ativo2-{Guid.NewGuid():N}@example.com", cancellationToken);
        await CreateCustomerAsync(client, accessToken, "Cliente Ativo 3", $"ativo3-{Guid.NewGuid():N}@example.com", cancellationToken);

        // Ativo sem e-mail — nao conta.
        await CreateCustomerAsync(client, accessToken, "Cliente Sem Email", null, cancellationToken);

        // Inativo com e-mail — nao conta.
        var inactiveId = await CreateCustomerAsync(
            client, accessToken, "Cliente Inativo", $"inativo-{Guid.NewGuid():N}@example.com", cancellationToken);
        (await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, accessToken, $"/api/customers/{inactiveId}/status", new { isActive = false }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/marketing/campanhas",
            new { subject = "Promocao de agosto", body = "Aproveite nossos descontos!" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("recipientCount").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task Sending_A_Campaign_Delivers_A_Real_Email_Via_Smtp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var customerEmail = $"campanha-{Guid.NewGuid():N}@example.com";
        await CreateCustomerAsync(client, accessToken, "Cliente Campanha", customerEmail, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/marketing/campanhas",
            new { subject = "Novidades do mes", body = "Confira as novidades." }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var found = await PollMailHogForAsync(customerEmail, cancellationToken, subjectContains: "Novidades do mes");
        found.ShouldBeTrue("a campanha deveria ter chegado no MailHog.");
    }

    [Fact]
    public async Task Listing_Campaigns_Should_Return_The_Sent_Campaign_Ordered_By_Most_Recent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        (await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/marketing/campanhas", new { subject = "Primeira campanha", body = "Corpo 1" }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/marketing/campanhas", new { subject = "Segunda campanha", body = "Corpo 2" }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/marketing/campanhas", cancellationToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        listBody.GetProperty("totalCount").GetInt32().ShouldBe(2);
        listBody.GetProperty("items")[0].GetProperty("subject").GetString().ShouldBe("Segunda campanha");
        listBody.GetProperty("items")[1].GetProperty("subject").GetString().ShouldBe("Primeira campanha");
    }

    [Fact]
    public async Task Sending_A_Campaign_With_Blank_Subject_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/marketing/campanhas", new { subject = "   ", body = "Corpo" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/marketing/campanhas", cancellationToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        listBody.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Campaigns_Are_Isolated_Between_Tenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        (await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantAToken, "/api/marketing/campanhas", new { subject = "Campanha do tenant A", body = "Corpo" }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantBList = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, tenantBToken, "/api/marketing/campanhas", cancellationToken);
        var tenantBBody = await tenantBList.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        tenantBBody.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    private async Task<bool> PollMailHogForAsync(string mustContain, CancellationToken cancellationToken, string? subjectContains = null)
    {
        using var mailHogClient = new HttpClient { BaseAddress = new Uri(fixture.MailHogApiBaseUrl) };
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var messagesJson = await mailHogClient.GetStringAsync("/api/v2/messages", cancellationToken);
            if (messagesJson.Contains(mustContain, StringComparison.OrdinalIgnoreCase)
                && (subjectContains is null || messagesJson.Contains(subjectContains, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            await Task.Delay(300, cancellationToken);
        }

        return false;
    }

    private static async Task<Guid> CreateCustomerAsync(
        HttpClient client, string accessToken, string fullName, string? email, CancellationToken cancellationToken)
    {
        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers", new { fullName, email }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<string> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
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

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return loginBody.GetProperty("accessToken").GetString()!;
    }
}
