using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// Cobre o perfil do estabelecimento (Fase 1 — fundacao): contato, endereco,
/// redes sociais e horario de funcionamento.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class TenantProfileTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Owner_Can_Update_And_Read_Back_The_Tenant_Profile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var updateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/profile",
            new
            {
                description = "Cortes classicos e modernos",
                phone = "11999998888",
                whatsApp = "11988887777",
                email = "contato@barbearia.com",
                address = "Rua das Flores, 100",
                instagramUrl = "https://instagram.com/barbearia",
                facebookUrl = "https://facebook.com/barbearia",
            },
            cancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var profileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/tenants/profile", cancellationToken);
        profileResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        profile.GetProperty("description").GetString().ShouldBe("Cortes classicos e modernos");
        profile.GetProperty("phone").GetString().ShouldBe("+5511999998888");
        profile.GetProperty("whatsApp").GetString().ShouldBe("+5511988887777");
        profile.GetProperty("email").GetString().ShouldBe("contato@barbearia.com");
        profile.GetProperty("address").GetString().ShouldBe("Rua das Flores, 100");
        profile.GetProperty("instagramUrl").GetString().ShouldBe("https://instagram.com/barbearia");
        profile.GetProperty("facebookUrl").GetString().ShouldBe("https://facebook.com/barbearia");
    }

    [Fact]
    public async Task Updating_Profile_With_An_Invalid_Email_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/profile", new { email = "nao-e-um-email" }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Owner_Can_Set_And_Read_Back_Business_Hours()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var setResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/business-hours",
            new
            {
                entries = new[]
                {
                    new { dayOfWeek = "Monday", startTime = "09:00:00", endTime = "18:00:00" },
                    new { dayOfWeek = "Saturday", startTime = "10:00:00", endTime = "14:00:00" },
                },
            },
            cancellationToken);
        setResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var profileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/tenants/profile", cancellationToken);
        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var businessHours = profile.GetProperty("businessHours");

        businessHours.GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Anonymous_Request_To_Get_Or_Update_Profile_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var getResponse = await client.GetAsync("/api/tenants/profile", cancellationToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var putResponse = await client.PutAsJsonAsync("/api/tenants/profile", new { description = "x" }, cancellationToken);
        putResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Staff_Member_Should_Not_Be_Able_To_Update_The_Tenant_Profile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, ownerToken) = await CreateTenantWithOwnerAndLoginAsyncWithId(client, cancellationToken);

        var staffEmail = $"staff-{Guid.NewGuid():N}@example.com";
        var inviteResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, ownerToken, "/api/team/invitations", new { email = staffEmail, role = "Staff" }, cancellationToken);
        var inviteBody = await inviteResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var token = inviteBody.GetProperty("token").GetString()!;
        await client.PostAsJsonAsync(
            $"/api/team/invitations/{token}/accept", new { fullName = "Funcionario", password = Password }, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = staffEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var staffToken = loginBody.GetProperty("accessToken").GetString()!;

        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, staffToken, "/api/tenants/profile", new { description = "Tentativa nao autorizada" }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<string> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var (_, accessToken) = await CreateTenantWithOwnerAndLoginAsyncWithId(client, cancellationToken);
        return accessToken;
    }

    private static async Task<(Guid TenantId, string AccessToken)> CreateTenantWithOwnerAndLoginAsyncWithId(
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
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono" }, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return (tenantId, loginBody.GetProperty("accessToken").GetString()!);
    }
}
