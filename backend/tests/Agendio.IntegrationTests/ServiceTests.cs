using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// CRUD de servicos, com atencao especial ao preco: Money e Value Object com
/// dois campos mapeado como owned type (ver ServiceConfiguration) — o teste
/// mais importante aqui e confirmar que ida e volta (criar -> ler) preserva o
/// valor exato, provando que o mapeamento owned type materializa direito.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class ServiceTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Owner_Can_Create_Update_And_List_Services_With_Correct_Price_Round_Trip()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/services",
            new { name = "Corte de Cabelo", description = "Corte na tesoura ou maquina", durationMinutes = 30, price = 45.90m, currency = "BRL", category = "Cabelo" },
            cancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var serviceId = createBody.GetProperty("id").GetGuid();

        var getResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/services/{serviceId}", cancellationToken);
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        getBody.GetProperty("name").GetString().ShouldBe("Corte de Cabelo");
        getBody.GetProperty("price").GetDecimal().ShouldBe(45.90m);
        getBody.GetProperty("currency").GetString().ShouldBe("BRL");
        getBody.GetProperty("durationMinutes").GetInt32().ShouldBe(30);

        var updateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/services/{serviceId}",
            new { name = "Corte de Cabelo Premium", description = "Corte + acabamento", durationMinutes = 45, price = 69.90m, currency = "BRL", category = "Cabelo" },
            cancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/services", cancellationToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        listBody.GetProperty("totalCount").GetInt32().ShouldBe(1);
        var item = listBody.GetProperty("items")[0];
        item.GetProperty("name").GetString().ShouldBe("Corte de Cabelo Premium");
        item.GetProperty("price").GetDecimal().ShouldBe(69.90m);
    }

    [Fact]
    public async Task Creating_A_Service_With_Zero_Duration_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/services",
            new { name = "Servico Invalido", durationMinutes = 0, price = 10m, currency = "BRL" },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_Tenant_Should_Never_See_A_Service_From_Another_Tenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantAToken, "/api/services",
            new { name = "Servico do Tenant A", durationMinutes = 30, price = 10m, currency = "BRL" },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var serviceId = createBody.GetProperty("id").GetGuid();

        var crossTenantGet = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, tenantBToken, $"/api/services/{serviceId}", cancellationToken);
        crossTenantGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
