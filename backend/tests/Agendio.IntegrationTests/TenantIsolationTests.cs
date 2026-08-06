using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Agendio.IntegrationTests;

/// <summary>
/// O teste mais importante do Sprint 0: prova que a defesa em profundidade
/// multi-tenant (claim do JWT + Global Query Filter + Row Level Security) de
/// fato isola dados entre tenants atraves da API HTTP real, contra um Postgres
/// real — nao um mock que assumiria a propria coisa que deveria provar.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class TenantIsolationTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task A_User_Registered_In_One_Tenant_Should_Be_Invisible_When_Logging_Into_Another_Tenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAId = await CreateTenantAsync(client, "Barbearia A", cancellationToken);
        var tenantBId = await CreateTenantAsync(client, "Clinica B", cancellationToken);

        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(client, tenantAId, email, Password, "Dono A", cancellationToken);

        // Mesmo e-mail e senha CORRETOS, mas apontando para o tenant ERRADO —
        // isso so pode falhar se o isolamento estiver funcionando.
        var crossTenantLogin = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId = tenantBId, email, password = Password }, cancellationToken);
        crossTenantLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Controle positivo: o MESMO login no tenant CERTO tem que funcionar —
        // prova que a falha acima e isolamento, nao um bug generico no login.
        var sameTenantLogin = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId = tenantAId, email, password = Password }, cancellationToken);
        sameTenantLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_Same_Email_Should_Be_Allowed_In_Two_Different_Tenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var tenantAId = await CreateTenantAsync(client, "Estudio A", cancellationToken);
        var tenantBId = await CreateTenantAsync(client, "Estudio B", cancellationToken);
        var email = $"compartilhado-{Guid.NewGuid():N}@example.com";

        var registerA = await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId = tenantAId, email, password = Password, fullName = "Pessoa A" }, cancellationToken);
        var registerB = await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId = tenantBId, email, password = "OutraSenhaForte456!", fullName = "Pessoa B" }, cancellationToken);

        registerA.StatusCode.ShouldBe(HttpStatusCode.Created);
        registerB.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Registering_The_Same_Email_Twice_In_The_Same_Tenant_Should_Fail()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var tenantId = await CreateTenantAsync(client, "Consultorio Unico", cancellationToken);
        var email = $"duplicado-{Guid.NewGuid():N}@example.com";

        var first = await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email, password = Password, fullName = "Primeira Conta" }, cancellationToken);
        var second = await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email, password = Password, fullName = "Segunda Conta" }, cancellationToken);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private static async Task<Guid> CreateTenantAsync(HttpClient client, string namePrefix, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync("/api/tenants", new
        {
            name = $"{namePrefix} {Guid.NewGuid():N}",
            slug = $"tenant-{Guid.NewGuid():N}",
            businessType = "Other",
            timeZoneId = "America/Sao_Paulo",
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task RegisterUserAsync(
        HttpClient client, Guid tenantId, string email, string password, string fullName, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email, password, fullName }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
