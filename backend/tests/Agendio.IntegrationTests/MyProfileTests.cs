using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>Cobre o autoatendimento de perfil do usuario autenticado (nome/telefone) — "Minha conta".</summary>
[Collection(nameof(IntegrationTestCollection))]
public class MyProfileTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task User_Can_Update_And_Read_Back_Their_Own_Name_And_Phone()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var updateResponse = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/auth/me", new { fullName = "Dono Atualizado", phone = "+5511977776666" }, cancellationToken);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var profileResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/auth/me", cancellationToken);
        profileResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        profile.GetProperty("fullName").GetString().ShouldBe("Dono Atualizado");
        profile.GetProperty("phone").GetString().ShouldBe("+5511977776666");
    }

    [Fact]
    public async Task Updating_My_Profile_With_An_Empty_Name_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var accessToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/auth/me", new { fullName = "", phone = (string?)null }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anonymous_Request_To_Update_My_Profile_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.PutAsJsonAsync("/api/auth/me", new { fullName = "x", phone = (string?)null }, cancellationToken);

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
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono", phone = "+5511999999999", cpfCnpj = "12345678909", termsAccepted = true }, cancellationToken);
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return loginBody.GetProperty("accessToken").GetString()!;
    }
}
