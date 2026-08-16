using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace Agendio.IntegrationTests;

/// <summary>
/// Cobre o rate limiting por tenant e a correcao de um gap real: login/registro
/// do tenant nao tinham a politica "auth" (10/min) aplicada, so o limite global
/// generoso — cada teste aqui usa sua propria factory com limites baixos
/// (WithWebHostBuilder), separada da factory padrao da suite (que mantem os
/// limites altos de proposito, ver IntegrationTestFixture.ConfigureWebHost).
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class RateLimitingTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Should_Rate_Limit_Login_By_Ip_When_No_Tenant_Claim_Present()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Registro acontece na factory padrao (limite alto) para nao consumir
        // a cota baixa que este teste vai exercitar no login.
        var setupClient = fixture.CreateClient();
        var (tenantId, email) = await CreateTenantWithOwnerAsync(setupClient, cancellationToken);

        using var lowLimitFactory = fixture.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:AuthPermitLimit", "3");
            builder.UseSetting("RateLimiting:AuthWindowSeconds", "60");
        });
        using var lowLimitClient = lowLimitFactory.CreateClient();

        HttpResponseMessage? lastResponse = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            lastResponse = await lowLimitClient.PostAsJsonAsync(
                "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        }

        lastResponse!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Should_Rate_Limit_Register_Endpoint_When_Policy_Not_Previously_Attached()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var lowLimitFactory = fixture.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:AuthPermitLimit", "3");
            builder.UseSetting("RateLimiting:AuthWindowSeconds", "60");
        });
        using var lowLimitClient = lowLimitFactory.CreateClient();

        HttpResponseMessage? lastResponse = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var tenantResponse = await lowLimitClient.PostAsJsonAsync("/api/tenants", new
            {
                name = $"Tenant {Guid.NewGuid():N}",
                slug = $"tenant-{Guid.NewGuid():N}",
                businessType = "Other",
                timeZoneId = "America/Sao_Paulo",
            }, cancellationToken);
            var tenantBody = await tenantResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var tenantId = tenantBody.GetProperty("id").GetGuid();

            lastResponse = await lowLimitClient.PostAsJsonAsync(
                "/api/auth/register",
                new { tenantId, email = $"user-{Guid.NewGuid():N}@example.com", password = Password, fullName = "Usuario" },
                cancellationToken);
        }

        lastResponse!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Should_Partition_Global_Limit_By_Tenant_When_Authenticated()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Setup (criar tenant, registrar, logar) acontece na factory padrao —
        // limite alto — porque essas chamadas sao anonimas e cairiam todas na
        // MESMA particao "ip:unknown" do limite global baixo abaixo, o que
        // esgotaria a cota antes mesmo do teste comecar a exercitar a
        // particao POR TENANT. O token JWT emitido aqui continua valido na
        // outra factory (mesma chave de assinatura, herdada do ConfigureWebHost
        // base) — so o estado do rate limiter e que e por-factory.
        var setupClient = fixture.CreateClient();
        var (tenantAId, tenantAOwnerEmail) = await CreateTenantWithOwnerAsync(setupClient, cancellationToken);
        var tenantAToken = await LoginAsync(setupClient, tenantAId, tenantAOwnerEmail, cancellationToken);

        var (tenantBId, tenantBOwnerEmail) = await CreateTenantWithOwnerAsync(setupClient, cancellationToken);
        var tenantBToken = await LoginAsync(setupClient, tenantBId, tenantBOwnerEmail, cancellationToken);

        using var lowLimitFactory = fixture.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:GlobalPermitLimit", "5");
            builder.UseSetting("RateLimiting:GlobalWindowSeconds", "60");
        });
        using var client = lowLimitFactory.CreateClient();

        // Esgota a particao (global, 5/min) do tenant A.
        HttpResponseMessage? lastTenantAResponse = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            lastTenantAResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, tenantAToken, "/api/team/members", cancellationToken);
        }

        lastTenantAResponse!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // Tenant B tem particao propria — nao afetado pelo esgotamento do A.
        var tenantBResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, tenantBToken, "/api/team/members", cancellationToken);
        tenantBResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<(Guid TenantId, string OwnerEmail)> CreateTenantWithOwnerAsync(HttpClient client, CancellationToken cancellationToken)
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
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono" }, cancellationToken);
        registerResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        return (tenantId, ownerEmail);
    }

    private static async Task<string> LoginAsync(HttpClient client, Guid tenantId, string email, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email, password = Password }, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("accessToken").GetString()!;
    }
}
