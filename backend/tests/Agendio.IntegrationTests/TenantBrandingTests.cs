using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// "Paleta de tema personalizada e rejeitada ao salvar se nao atingir
/// contraste AA" e regra do projeto (CLAUDE.md) — este teste e a prova de que
/// o backend, nao so a UI, aplica essa regra.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class TenantBrandingTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Owner_Can_Update_Branding_With_A_Color_That_Has_Enough_Contrast()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (_, slug, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/branding", new { primaryColorHex = "#1F2937" }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var tenantResponse = await client.GetAsync($"/api/tenants/by-slug/{slug}", cancellationToken);
        var tenantBody = await tenantResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        tenantBody.GetProperty("primaryColorHex").GetString().ShouldBe("#1F2937");
    }

    [Fact]
    public async Task Updating_Branding_With_Insufficient_Contrast_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (_, _, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        // Amarelo claro: falha o contraste AA contra o texto branco que a UI sempre usa.
        var response = await AuthorizedRequestHelpers.PutAuthorizedAsync(
            client, accessToken, "/api/tenants/branding", new { primaryColorHex = "#FFFF00" }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Anonymous_Request_To_Update_Branding_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.PutAsJsonAsync("/api/tenants/branding", new { primaryColorHex = "#1F2937" }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<(Guid TenantId, string Slug, string AccessToken)> CreateTenantWithOwnerAndLoginAsync(
        HttpClient client, CancellationToken cancellationToken)
    {
        var slug = $"tenant-{Guid.NewGuid():N}";

        var tenantResponse = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"Tenant {Guid.NewGuid():N}",
            slug,
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

        return (tenantId, slug, loginBody.GetProperty("accessToken").GetString()!);
    }
}
