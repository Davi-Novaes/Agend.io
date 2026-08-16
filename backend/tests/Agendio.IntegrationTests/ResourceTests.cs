using System.Net;
using System.Net.Http.Headers;
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
    public async Task Creating_A_Resource_Without_A_UnitId_Should_Succeed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Sala Unica", type = "Room", capacity = 1, description = (string?)null },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Creating_A_Resource_With_A_UnitId_From_Another_Tenant_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var unitResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantAToken, "/api/units", new { name = "Unidade do Tenant A", address = (string?)null }, cancellationToken);
        var unitBody = await unitResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var unitId = unitBody.GetProperty("id").GetGuid();

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantBToken, "/api/resources",
            new { name = "Recurso", type = "Room", capacity = 1, description = (string?)null, unitId },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Creating_A_Resource_With_A_Valid_UnitId_Should_Succeed_And_Roundtrip()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var unitResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/units", new { name = "Unidade Centro", address = (string?)null }, cancellationToken);
        var unitBody = await unitResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var unitId = unitBody.GetProperty("id").GetGuid();

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Cadeira 1", type = "Room", capacity = 1, description = (string?)null, unitId },
            cancellationToken);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = createBody.GetProperty("id").GetGuid();

        var getResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}", cancellationToken);
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        getBody.GetProperty("unitId").GetGuid().ShouldBe(unitId);
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

    [Fact]
    public async Task Owner_Can_Upload_A_Resource_Photo_And_Then_Fetch_It_Back()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Dra. Ana", type = "Person", capacity = 1, description = (string?)null }, cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = createBody.GetProperty("id").GetGuid();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TinyPng);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "foto.png");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/resources/{resourceId}/photo") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var uploadResponse = await client.SendAsync(request, cancellationToken);

        uploadResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var photoUrl = uploadBody.GetProperty("photoUrl").GetString()!;
        photoUrl.ShouldStartWith("/uploads/resource-photos/");

        var getResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, $"/api/resources/{resourceId}", cancellationToken);
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        getBody.GetProperty("photoUrl").GetString().ShouldBe(photoUrl);
    }

    [Fact]
    public async Task Owner_Can_Set_And_Replace_Resource_Specialties()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Dra. Ana", type = "Person", capacity = 1, description = (string?)null }, cancellationToken);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = createBody.GetProperty("id").GetGuid();

        var setResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/specialties",
            new { specialties = new[] { "Ortodontia", "Clareamento", "  ", "Ortodontia" } }, cancellationToken);
        setResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, $"/api/resources/{resourceId}", cancellationToken);
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var specialties = getBody.GetProperty("specialties").EnumerateArray().Select(s => s.GetString()).ToList();
        specialties.ShouldBe(["Ortodontia", "Clareamento"]);

        // Substitui a lista inteira — nao acumula.
        await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/specialties", new { specialties = new[] { "Implantes" } }, cancellationToken);
        var getAfterResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, $"/api/resources/{resourceId}", cancellationToken);
        var getAfterBody = await getAfterResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        getAfterBody.GetProperty("specialties").EnumerateArray().Select(s => s.GetString()).ToList().ShouldBe(["Implantes"]);
    }

    [Fact]
    public async Task Owner_Can_Set_And_Replace_Resource_Services()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Dra. Ana", type = "Person", capacity = 1, description = (string?)null }, cancellationToken);
        var resourceBody = await createResourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = resourceBody.GetProperty("id").GetGuid();

        var serviceAId = await CreateServiceAsync(client, accessToken, "Ortodontia", cancellationToken);
        var serviceBId = await CreateServiceAsync(client, accessToken, "Clareamento", cancellationToken);

        var setResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/services",
            new { serviceIds = new[] { serviceAId, serviceBId, serviceAId } }, cancellationToken);
        setResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var getResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, $"/api/resources/{resourceId}", cancellationToken);
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var serviceIds = getBody.GetProperty("serviceIds").EnumerateArray().Select(s => s.GetGuid()).ToList();
        serviceIds.ShouldBe([serviceAId, serviceBId], ignoreOrder: true);

        // Substitui a lista inteira — nao acumula.
        await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/services", new { serviceIds = new[] { serviceAId } }, cancellationToken);
        var getAfterResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, $"/api/resources/{resourceId}", cancellationToken);
        var getAfterBody = await getAfterResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        getAfterBody.GetProperty("serviceIds").EnumerateArray().Select(s => s.GetGuid()).ToList().ShouldBe([serviceAId]);
    }

    [Fact]
    public async Task Setting_A_Nonexistent_ServiceId_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Dra. Ana", type = "Person", capacity = 1, description = (string?)null }, cancellationToken);
        var resourceBody = await createResourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = resourceBody.GetProperty("id").GetGuid();

        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/services",
            new { serviceIds = new[] { Guid.NewGuid() } }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Setting_A_ServiceId_From_Another_Tenant_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var serviceIdFromTenantA = await CreateServiceAsync(client, tenantAToken, "Ortodontia", cancellationToken);

        var createResourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantBToken, "/api/resources",
            new { name = "Recurso do Tenant B", type = "Person", capacity = 1, description = (string?)null }, cancellationToken);
        var resourceBody = await createResourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = resourceBody.GetProperty("id").GetGuid();

        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, tenantBToken, $"/api/resources/{resourceId}/services",
            new { serviceIds = new[] { serviceIdFromTenantA } }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<Guid> CreateServiceAsync(HttpClient client, string accessToken, string name, CancellationToken cancellationToken)
    {
        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/services",
            new { name, durationMinutes = 30, price = 45.90m, currency = "BRL" }, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Owner_Can_Create_List_And_Delete_TimeOff()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Dra. Ana", type = "Person", capacity = 1, description = (string?)null }, cancellationToken);
        var resourceBody = await createResourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = resourceBody.GetProperty("id").GetGuid();

        var createTimeOffResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/time-off",
            new { startDate = "2026-08-20", endDate = "2026-08-22", reason = "Ferias" }, cancellationToken);
        createTimeOffResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createTimeOffBody = await createTimeOffResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var timeOffId = createTimeOffBody.GetProperty("id").GetGuid();

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/time-off", cancellationToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        listBody.GetArrayLength().ShouldBe(1);
        listBody[0].GetProperty("reason").GetString().ShouldBe("Ferias");

        var deleteResponse = await AuthorizedRequestHelpers.DeleteAuthorizedAsync(
            client, accessToken, $"/api/resources/time-off/{timeOffId}", cancellationToken);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var listAfterDeleteResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/time-off", cancellationToken);
        var listAfterDeleteBody = await listAfterDeleteResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        listAfterDeleteBody.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Creating_A_TimeOff_With_EndDate_Before_StartDate_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Dra. Ana", type = "Person", capacity = 1, description = (string?)null }, cancellationToken);
        var resourceBody = await createResourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = resourceBody.GetProperty("id").GetGuid();

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, $"/api/resources/{resourceId}/time-off",
            new { startDate = "2026-08-22", endDate = "2026-08-20", reason = (string?)null }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_Tenant_Should_Never_See_Or_Delete_TimeOff_From_Another_Tenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantBToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var createResourceResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantAToken, "/api/resources",
            new { name = "Recurso do Tenant A", type = "Person", capacity = 1, description = (string?)null }, cancellationToken);
        var resourceBody = await createResourceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resourceId = resourceBody.GetProperty("id").GetGuid();

        var createTimeOffResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantAToken, $"/api/resources/{resourceId}/time-off",
            new { startDate = "2026-08-20", endDate = "2026-08-20", reason = (string?)null }, cancellationToken);
        var createTimeOffBody = await createTimeOffResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var timeOffId = createTimeOffBody.GetProperty("id").GetGuid();

        var crossTenantDelete = await AuthorizedRequestHelpers.DeleteAuthorizedAsync(
            client, tenantBToken, $"/api/resources/time-off/{timeOffId}", cancellationToken);
        crossTenantDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var crossTenantList = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, tenantBToken, $"/api/resources/{resourceId}/time-off", cancellationToken);
        var crossTenantListBody = await crossTenantList.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        crossTenantListBody.GetArrayLength().ShouldBe(0);
    }

    // Menor PNG valido possivel (1x1 transparente) — bytes reais, nao um mock.
    private static readonly byte[] TinyPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82,
    ];

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
