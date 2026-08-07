using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Platform.Domain;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.IntegrationTests;

/// <summary>
/// O Super Admin e uma autoridade SEPARADA de qualquer tenant (ver CLAUDE.md).
/// Sem endpoint publico para criar PlatformAdmin de proposito (provisionamento e
/// so via seed de Development, fora do MVP para producao) — os testes inserem o
/// admin diretamente via PlatformDbContext, o mesmo padrao ja usado em
/// AppointmentNotificationTests para acessar infraestrutura que nao tem rota HTTP.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class PlatformTests(IntegrationTestFixture fixture)
{
    private const string AdminPassword = "SenhaDoAdmin123!";

    [Fact]
    public async Task Platform_Admin_Can_Login_With_Valid_Credentials()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var email = await SeedPlatformAdminAsync(cancellationToken);

        var response = await client.PostAsJsonAsync("/api/platform/auth/login", new { email, password = AdminPassword }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("accessToken").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Platform_Admin_Login_With_Wrong_Password_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var email = await SeedPlatformAdminAsync(cancellationToken);

        var response = await client.PostAsJsonAsync("/api/platform/auth/login", new { email, password = "senha-errada" }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Listing_Tenants_Without_A_Platform_Token_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var anonymousResponse = await client.GetAsync("/api/platform/tenants", cancellationToken);
        anonymousResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Token de TENANT (autoridade completamente diferente — outro issuer,
        // outra chave) nunca deveria autenticar aqui, mesmo enviado como Bearer.
        var tenantToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantTokenResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, tenantToken, "/api/platform/tenants", cancellationToken);
        tenantTokenResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Platform_Admin_Can_List_And_Deactivate_A_Tenant_Which_Then_Blocks_Its_Users_Login()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var adminEmail = await SeedPlatformAdminAsync(cancellationToken);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/platform/auth/login", new { email = adminEmail, password = AdminPassword }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var platformToken = loginBody.GetProperty("accessToken").GetString()!;

        var (tenantId, tenantOwnerEmail) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var listResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, platformToken, "/api/platform/tenants", cancellationToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenants = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        tenants.EnumerateArray().ShouldContain(t => t.GetProperty("id").GetGuid() == tenantId);

        var deactivateResponse = await AuthorizedRequestHelpers.PatchAuthorizedAsync(
            client, platformToken, $"/api/platform/tenants/{tenantId}/status", new { isActive = false }, cancellationToken);
        deactivateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // A checagem de Tenant.IsActive ja existe em LoginCommandHandler desde
        // antes do Sprint 6 — desativar aqui tem que bloquear login imediatamente,
        // sem precisar de nenhuma mudanca no modulo Identity.
        var blockedLoginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { tenantId, email = tenantOwnerEmail, password = "SenhaForte123!" },
            cancellationToken);
        blockedLoginResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<string> SeedPlatformAdminAsync(CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var email = $"admin-{Guid.NewGuid():N}@example.com";
        var admin = PlatformAdmin.Create(email, "Admin de Teste", passwordHasher.Hash(AdminPassword)).Value;
        dbContext.PlatformAdmins.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);

        return email;
    }

    private static async Task<(Guid TenantId, string OwnerEmail)> CreateTenantWithOwnerAsync(HttpClient client, CancellationToken cancellationToken)
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
            "/api/auth/register", new { tenantId, email = ownerEmail, password = "SenhaForte123!", fullName = "Dono" }, cancellationToken);

        return (tenantId, ownerEmail);
    }

    private static async Task<string> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var (tenantId, ownerEmail) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = "SenhaForte123!" }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return loginBody.GetProperty("accessToken").GetString()!;
    }
}
