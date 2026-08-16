using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// CRUD de unidades (lojas/filiais) do tenant. Sem paginacao — negocio-alvo tem
/// poucas unidades cadastradas.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class UnitsTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Owner_Can_Create_Update_Deactivate_And_List_Units()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/units", new { name = "Unidade Centro", address = "Rua Principal, 123" }, cancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var unitId = createBody.GetProperty("id").GetGuid();

        var getResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, $"/api/units/{unitId}", cancellationToken);
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        getBody.GetProperty("name").GetString().ShouldBe("Unidade Centro");
        getBody.GetProperty("isActive").GetBoolean().ShouldBeTrue();

        var updateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/units/{unitId}", new { name = "Unidade Centro Renovada", address = "Nova Rua, 456" }, cancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var deactivateResponse = await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, accessToken, $"/api/units/{unitId}/status", new { isActive = false }, cancellationToken);
        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/units", cancellationToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        listBody.GetArrayLength().ShouldBe(1);
        listBody[0].GetProperty("name").GetString().ShouldBe("Unidade Centro Renovada");
        listBody[0].GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Creating_A_Unit_With_Blank_Name_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/units", new { name = "   ", address = (string?)null }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_Tenant_Should_Never_See_Or_Reuse_A_Unit_From_Another_Tenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantAToken, "/api/units", new { name = "Unidade do Tenant A", address = (string?)null }, cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var unitId = createBody.GetProperty("id").GetGuid();

        var crossTenantGet = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, tenantBToken, $"/api/units/{unitId}", cancellationToken);
        crossTenantGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var crossTenantList = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, tenantBToken, "/api/units", cancellationToken);
        var crossTenantListBody = await crossTenantList.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        crossTenantListBody.GetArrayLength().ShouldBe(0);

        // O tenant B tentando cadastrar um recurso apontando pro UnitId do tenant A
        // deve ser rejeitado — cobre o mesmo caminho que ResourceTests.
        var resourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantBToken, "/api/resources",
            new { name = "Recurso", type = "Room", capacity = 1, description = (string?)null, unitId },
            cancellationToken);
        resourceResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anonymous_Request_To_List_Units_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/units", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<string> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
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

        return loginBody.GetProperty("accessToken").GetString()!;
    }
}
