using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// CRUD de recursos (pessoa/sala/equipamento) e horarios de trabalho. O ponto
/// mais delicado aqui e resource_working_hours: uma colecao owned SEM tenant_id
/// proprio, protegida por RLS via subquery no dono (ver migration) — o teste de
/// isolamento cruzado cobre justamente esse caminho.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class ResourceTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Owner_Can_Create_Update_And_List_Resources()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Dra. Ana", type = "Person", capacity = 1, description = "Dentista" },
            cancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = createBody.GetProperty("id").GetGuid();

        var getResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}", cancellationToken);
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        getBody.GetProperty("name").GetString().ShouldBe("Dra. Ana");
        getBody.GetProperty("type").GetString().ShouldBe("Person");
        getBody.GetProperty("capacity").GetInt32().ShouldBe(1);
        getBody.GetProperty("workingHours").GetArrayLength().ShouldBe(0);

        var updateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}",
            new { name = "Dra. Ana Costa", type = "Person", capacity = 1, description = "Dentista - ortodontia" },
            cancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/resources", cancellationToken);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        listBody.GetProperty("totalCount").GetInt32().ShouldBe(1);
        listBody.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("Dra. Ana Costa");
    }

    [Fact]
    public async Task Owner_Can_Set_Working_Hours_And_Read_Them_Back_Ordered()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Sala 1", type = "Room", capacity = 1, description = (string?)null },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = createBody.GetProperty("id").GetGuid();

        var setHoursResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/working-hours",
            new
            {
                entries = new[]
                {
                    new { dayOfWeek = "Wednesday", startTime = "09:00:00", endTime = "12:00:00" },
                    new { dayOfWeek = "Monday", startTime = "09:00:00", endTime = "18:00:00" },
                    new { dayOfWeek = "Monday", startTime = "19:00:00", endTime = "21:00:00" },
                },
            },
            cancellationToken);
        setHoursResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}", cancellationToken);
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var workingHours = getBody.GetProperty("workingHours");

        workingHours.GetArrayLength().ShouldBe(3);
        workingHours[0].GetProperty("dayOfWeek").GetString().ShouldBe("Monday");
        workingHours[0].GetProperty("startTime").GetString().ShouldBe("09:00:00");
        workingHours[1].GetProperty("dayOfWeek").GetString().ShouldBe("Monday");
        workingHours[1].GetProperty("startTime").GetString().ShouldBe("19:00:00");
        workingHours[2].GetProperty("dayOfWeek").GetString().ShouldBe("Wednesday");
    }

    [Fact]
    public async Task Setting_Working_Hours_With_End_Before_Start_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Sala 2", type = "Room", capacity = 1, description = (string?)null },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = createBody.GetProperty("id").GetGuid();

        var setHoursResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/working-hours",
            new { entries = new[] { new { dayOfWeek = "Monday", startTime = "18:00:00", endTime = "09:00:00" } } },
            cancellationToken);

        setHoursResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Creating_A_Resource_With_Zero_Capacity_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Recurso Invalido", type = "Room", capacity = 0, description = (string?)null },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_Tenant_Should_Never_See_A_Resource_Or_Its_Working_Hours_From_Another_Tenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantAToken, "/api/resources",
            new { name = "Recurso do Tenant A", type = "Equipment", capacity = 1, description = (string?)null },
            cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = createBody.GetProperty("id").GetGuid();

        await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, tenantAToken, $"/api/resources/{resourceId}/working-hours",
            new { entries = new[] { new { dayOfWeek = "Monday", startTime = "09:00:00", endTime = "18:00:00" } } },
            cancellationToken);

        var crossTenantGet = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, tenantBToken, $"/api/resources/{resourceId}", cancellationToken);
        crossTenantGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var crossTenantSetHours = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, tenantBToken, $"/api/resources/{resourceId}/working-hours",
            new { entries = Array.Empty<object>() },
            cancellationToken);
        crossTenantSetHours.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Anonymous_Request_To_List_Resources_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/resources", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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
