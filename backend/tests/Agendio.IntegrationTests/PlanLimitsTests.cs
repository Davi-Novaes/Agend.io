using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.Modules.Billing.Infrastructure.Persistence.Configurations;
using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.IntegrationTests;

/// <summary>
/// Cobre o limite de plano (unidades/profissionais/clientes) introduzido junto
/// com os planos pagos reais (Essencial/Profissional/Premium) — ver
/// IPlanLimitsLookupService (Billing.Contracts) e os handlers de criacao de
/// Tenancy/Resources/Customers.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class PlanLimitsTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Creating_A_Unit_Beyond_The_Plan_Limit_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (_, accessToken) = await CreateTenantOnPlanAsync(client, PlanConfiguration.EssencialPlanId, cancellationToken);

        // Essencial permite 1 unidade.
        var firstResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/units", new { name = "Unidade 1", address = (string?)null }, cancellationToken);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var secondResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/units", new { name = "Unidade 2", address = (string?)null }, cancellationToken);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Creating_A_Professional_Beyond_The_Plan_Limit_Should_Be_Rejected_But_Rooms_Are_Unaffected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (_, accessToken) = await CreateTenantOnPlanAsync(client, PlanConfiguration.EssencialPlanId, cancellationToken);

        // Essencial permite 3 profissionais (ResourceType.Person).
        for (var i = 0; i < 3; i++)
        {
            var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
                client, accessToken, "/api/resources",
                new { name = $"Profissional {i}", type = "Person", capacity = 1, description = (string?)null, unitId = (Guid?)null },
                cancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var overLimitResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Profissional 4", type = "Person", capacity = 1, description = (string?)null, unitId = (Guid?)null },
            cancellationToken);
        overLimitResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Sala/equipamento nao entram na conta de profissionais — nunca bloqueados por esse limite.
        var roomResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/resources",
            new { name = "Sala 1", type = "Room", capacity = 1, description = (string?)null, unitId = (Guid?)null },
            cancellationToken);
        roomResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Creating_A_Customer_Beyond_The_Plan_Limit_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantOnPlanAsync(client, PlanConfiguration.EssencialPlanId, cancellationToken);

        // Essencial permite 300 clientes — inserir os 300 direto no banco
        // (bulk) em vez de 300 chamadas HTTP, so a ultima (a 301a) precisa
        // passar pelo handler de verdade pra provar que o limite bloqueia.
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
            // RLS exige a sessao do Postgres marcada com o tenant certo, senao
            // o insert direto (sem passar pelo handler) e rejeitado — mesma
            // defesa que protege contra vazamento entre tenants em producao.
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.SetTenant(TenantId.From(tenantId));

            for (var i = 0; i < 300; i++)
            {
                var customerResult = Customer.Create(TenantId.From(tenantId), $"Cliente {i}", null, null, null, null);
                dbContext.Customers.Add(customerResult.Value);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var overLimitResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/customers", new { fullName = "Cliente 301" }, cancellationToken);
        overLimitResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_Legacy_Subscription_On_A_Plan_Without_Limits_Should_Never_Be_Blocked()
    {
        // Gratis e Padrao foram desativados (somem do catalogo publico —
        // ninguem escolhe mais nenhum dos dois no onboarding), mas continuam
        // sem nenhum limite. Um tenant legado cuja assinatura ainda aponte
        // pra um dos dois (nunca migrada) nao pode ficar bloqueado por um
        // limite que nunca existiu pra ele -- monta esse cenario direto no
        // banco, ja que a rota publica nao aceita mais selecionar Gratis.
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

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
            "/api/auth/register",
            new { tenantId, email = ownerEmail, password = Password, fullName = "Dono", phone = "+5511999999999", cpfCnpj = "12345678909", termsAccepted = true },
            cancellationToken);
        registerResponse.EnsureSuccessStatusCode();
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var subscription = Subscription.StartTrial(TenantId.From(tenantId), PlanConfiguration.FreePlanId, DateTimeOffset.UtcNow);
            dbContext.Subscriptions.Add(subscription);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var accessToken = loginBody.GetProperty("accessToken").GetString()!;

        for (var i = 0; i < 2; i++)
        {
            var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
                client, accessToken, "/api/units", new { name = $"Unidade {i}", address = (string?)null }, cancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
        }
    }

    private async Task<(Guid TenantId, string AccessToken)> CreateTenantOnPlanAsync(
        HttpClient client, PlanId planId, CancellationToken cancellationToken)
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
            "/api/auth/register",
            new { tenantId, email = ownerEmail, password = Password, fullName = "Dono", phone = "+5511999999999", cpfCnpj = "12345678909", termsAccepted = true },
            cancellationToken);
        registerResponse.EnsureSuccessStatusCode();
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var onboardingToken = registerBody.GetProperty("onboardingToken").GetString()!;

        var selectPlanResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, onboardingToken, "/api/billing/subscription/onboard-select-plan",
            new { planId = planId.Value }, cancellationToken);
        selectPlanResponse.EnsureSuccessStatusCode();

        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return (tenantId, loginBody.GetProperty("accessToken").GetString()!);
    }
}
