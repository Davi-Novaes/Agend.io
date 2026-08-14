using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// CRUD de clientes + o teste mais importante do Sprint 2: um tenant nunca
/// enxerga cliente de outro (RLS + Global Query Filter), mesmo sabendo o Id exato.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class CustomerTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Owner_Can_Create_Update_And_List_Customers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers",
            new { fullName = "Maria Silva", email = "maria@example.com", phone = "11999998888", notes = "Prefere manha" },
            cancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var customerId = createBody.GetProperty("id").GetGuid();

        var updateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/customers/{customerId}",
            new { fullName = "Maria Silva Santos", email = "maria@example.com", phone = "11999998888", notes = "Prefere tarde" },
            cancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/customers/{customerId}", cancellationToken);
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        getBody.GetProperty("fullName").GetString().ShouldBe("Maria Silva Santos");
        getBody.GetProperty("notes").GetString().ShouldBe("Prefere tarde");

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, "/api/customers?search=Maria", cancellationToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        listBody.GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task A_Tenant_Should_Never_See_A_Customer_From_Another_Tenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantAToken, "/api/customers", new { fullName = "Cliente do Tenant A" }, cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var customerId = createBody.GetProperty("id").GetGuid();

        // Mesmo Id exato, token do OUTRO tenant: RLS + Global Query Filter tem
        // que devolver 404, nunca o cliente de verdade.
        var crossTenantGet = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, tenantBToken, $"/api/customers/{customerId}", cancellationToken);
        crossTenantGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var tenantBList = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, tenantBToken, "/api/customers", cancellationToken);
        var tenantBListBody = await tenantBList.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        tenantBListBody.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Importing_A_Csv_Should_Create_Valid_Rows_And_Report_Invalid_Ones()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        const string csv = "FullName,Email,Phone\nJoao Souza,joao@example.com,11988887777\n,semnome@example.com,\nAna Lima,,11977776666\n";
        var csvBytes = Encoding.UTF8.GetBytes(csv);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(csvBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "clientes.csv");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/customers/import") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("imported").GetInt32().ShouldBe(2);
        body.GetProperty("skipped").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Creating_And_Updating_A_Customer_Should_Record_Audit_Log_Entries()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers",
            new { fullName = "Joana Alves", email = "joana@example.com" },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var customerId = createBody.GetProperty("id").GetGuid();

        await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/customers/{customerId}",
            new { fullName = "Joana Alves Costa", email = "joana@example.com" },
            cancellationToken);

        var auditResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/customers/{customerId}/audit-log", cancellationToken);
        auditResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var auditBody = await auditResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        auditBody.GetProperty("totalCount").GetInt32().ShouldBe(2);

        var items = auditBody.GetProperty("items").EnumerateArray().ToList();
        items.ShouldContain(e => e.GetProperty("action").GetString() == "Created");
        items.ShouldContain(e => e.GetProperty("action").GetString() == "Updated");

        var updated = items.Single(e => e.GetProperty("action").GetString() == "Updated");
        updated.GetProperty("after").GetString().ShouldNotBeNull().ShouldContain("Joana Alves Costa");
        updated.GetProperty("performedBy").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_Tenant_Should_Never_See_Audit_Log_Entries_From_Another_Tenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantAToken, "/api/customers", new { fullName = "Cliente do Tenant A" }, cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var customerId = createBody.GetProperty("id").GetGuid();

        var crossTenantAudit = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, tenantBToken, $"/api/customers/{customerId}/audit-log", cancellationToken);
        crossTenantAudit.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Anonymous_Request_To_List_Customers_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/customers", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task New_Customer_Without_Appointments_Should_Be_Segmented_As_Novo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers", new { fullName = "Cliente Sem Historico" }, cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var customerId = createBody.GetProperty("id").GetGuid();

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, "/api/customers?search=" + Uri.EscapeDataString("Cliente Sem Historico"), cancellationToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var item = listBody.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == customerId);
        item.GetProperty("segment").GetString().ShouldBe("Novo");
    }

    [Fact]
    public async Task Customer_With_A_Completed_Visit_Should_Be_Segmented_As_Recorrente_And_Filterable()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var customerResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers", new { fullName = "Cliente Recorrente" }, cancellationToken);
        var customerId = (await customerResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        var resourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources", new { name = "Cadeira 1", type = "Room", capacity = 1, description = (string?)null }, cancellationToken);
        var resourceId = (await resourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        var serviceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/services",
            new { name = "Corte", description = (string?)null, durationMinutes = 30, price = 45.90m, currency = "BRL", category = (string?)null },
            cancellationToken);
        var serviceId = (await serviceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        var appointmentResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/appointments",
            new { customerId, resourceId, serviceId, startAtUtc = DateTimeOffset.UtcNow.AddDays(1), notes = (string?)null },
            cancellationToken);
        var appointmentId = (await appointmentResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/confirm", new { }, cancellationToken);
        await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/start", new { }, cancellationToken);
        (await AuthorizedRequestHelpers.PostAuthorizedAsync(client, accessToken, $"/api/appointments/{appointmentId}/complete", new { }, cancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, "/api/customers?search=" + Uri.EscapeDataString("Cliente Recorrente"), cancellationToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var item = listBody.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == customerId);
        item.GetProperty("segment").GetString().ShouldBe("Recorrente");

        var filteredResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, "/api/customers?segment=Recorrente", cancellationToken);
        var filteredBody = await filteredResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        filteredBody.GetProperty("items").EnumerateArray().ShouldContain(i => i.GetProperty("id").GetGuid() == customerId);

        var wrongFilterResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, "/api/customers?segment=Novo", cancellationToken);
        var wrongFilterBody = await wrongFilterResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        wrongFilterBody.GetProperty("items").EnumerateArray().ShouldNotContain(i => i.GetProperty("id").GetGuid() == customerId);
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
