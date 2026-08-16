using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendio.Infrastructure.Messaging;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.Modules.Platform.Domain;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task Platform_Dashboard_Without_A_Platform_Token_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var anonymousResponse = await client.GetAsync("/api/platform/dashboard", cancellationToken);
        anonymousResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var tenantToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantTokenResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, tenantToken, "/api/platform/dashboard", cancellationToken);
        tenantTokenResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Platform_Dashboard_Reflects_A_New_Tenant_And_Its_Trialing_Subscription()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var platformToken = await LoginAsPlatformAdminAsync(client, cancellationToken);

        var baseline = await GetDashboardMetricsAsync(client, platformToken, cancellationToken);

        var (tenantId, _) = await CreateTenantWithOwnerAsync(client, cancellationToken);
        await ForceDrainTenancyOutboxAsync(cancellationToken);
        await WaitForSubscriptionAsync(tenantId, cancellationToken);

        var afterMetrics = await GetDashboardMetricsAsync(client, platformToken, cancellationToken);

        // Metricas sao globais (todos os tenants) e o consumidor de RabbitMQ
        // roda em background, fora do controle deste teste — outros testes da
        // mesma suite podem ter tenants/assinaturas concluindo entre o
        // baseline e a segunda leitura. A asserção verifica que os contadores
        // cresceram PELO MENOS o esperado (nunca diminuem), nao um delta exato.
        (afterMetrics.GetProperty("totalTenants").GetInt32() - baseline.GetProperty("totalTenants").GetInt32()).ShouldBeGreaterThanOrEqualTo(1);
        (afterMetrics.GetProperty("activeTenants").GetInt32() - baseline.GetProperty("activeTenants").GetInt32()).ShouldBeGreaterThanOrEqualTo(1);
        (afterMetrics.GetProperty("newTenantsThisMonth").GetInt32() - baseline.GetProperty("newTenantsThisMonth").GetInt32()).ShouldBeGreaterThanOrEqualTo(1);
        (afterMetrics.GetProperty("trialingCount").GetInt32() - baseline.GetProperty("trialingCount").GetInt32()).ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Platform_Dashboard_Mrr_Reflects_An_Active_Subscriptions_Plan_Price()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var platformToken = await LoginAsPlatformAdminAsync(client, cancellationToken);

        var baseline = await GetDashboardMetricsAsync(client, platformToken, cancellationToken);

        var (tenantId, _) = await CreateTenantWithOwnerAsync(client, cancellationToken);
        await ForceDrainTenancyOutboxAsync(cancellationToken);
        var subscription = await WaitForSubscriptionAsync(tenantId, cancellationToken);

        decimal planPriceAmount;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var trackedSubscription = await dbContext.Subscriptions.SingleAsync(s => s.Id == subscription.Id, cancellationToken);
            var plan = await dbContext.Plans.SingleAsync(p => p.Id == trackedSubscription.PlanId, cancellationToken);
            planPriceAmount = plan.PriceAmount;

            trackedSubscription.MarkActive(DateTimeOffset.UtcNow.AddDays(30));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var afterMetrics = await GetDashboardMetricsAsync(client, platformToken, cancellationToken);

        // Mesma ressalva do teste anterior: outra assinatura pode ter sido
        // ativada por outro teste no meio do caminho, entao o MRR so pode ter
        // crescido pelo menos o preco do plano que acabamos de ativar aqui.
        (afterMetrics.GetProperty("activeSubscriptionsCount").GetInt32() - baseline.GetProperty("activeSubscriptionsCount").GetInt32()).ShouldBeGreaterThanOrEqualTo(1);
        (afterMetrics.GetProperty("mrr").GetDecimal() - baseline.GetProperty("mrr").GetDecimal()).ShouldBeGreaterThanOrEqualTo(planPriceAmount);
        afterMetrics.GetProperty("mrrCurrency").GetString().ShouldBe("BRL");
    }

    [Fact]
    public async Task Cancelling_A_Subscription_Without_A_Platform_Token_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var anonymousResponse = await client.PostAsync($"/api/platform/subscriptions/{Guid.NewGuid()}/cancel", content: null, cancellationToken);
        anonymousResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var tenantToken = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var tenantTokenResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tenantToken, $"/api/platform/subscriptions/{Guid.NewGuid()}/cancel", new { }, cancellationToken);
        tenantTokenResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Super_Admin_Can_Cancel_A_Tenants_Subscription_But_Not_Twice()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var platformToken = await LoginAsPlatformAdminAsync(client, cancellationToken);

        var (tenantId, _) = await CreateTenantWithOwnerAsync(client, cancellationToken);
        await ForceDrainTenancyOutboxAsync(cancellationToken);
        await WaitForSubscriptionAsync(tenantId, cancellationToken);

        var cancelResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, platformToken, $"/api/platform/subscriptions/{tenantId}/cancel", new { }, cancellationToken);
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var subscription = await dbContext.Subscriptions.SingleAsync(s => s.TenantId == TenantId.From(tenantId), cancellationToken);
            subscription.Status.ShouldBe(SubscriptionStatus.Canceled);
        }

        // Acao irreversivel: cancelar de novo nao pode "ter sucesso silencioso"
        // nem duplicar efeito — Subscription.Cancel ja rejeita, o handler so repassa.
        var secondCancelResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, platformToken, $"/api/platform/subscriptions/{tenantId}/cancel", new { }, cancellationToken);
        secondCancelResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancelling_A_Subscription_For_An_Unknown_Tenant_Should_Be_NotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var platformToken = await LoginAsPlatformAdminAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, platformToken, $"/api/platform/subscriptions/{Guid.NewGuid()}/cancel", new { }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<string> LoginAsPlatformAdminAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var adminEmail = await SeedPlatformAdminAsync(cancellationToken);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/platform/auth/login", new { email = adminEmail, password = AdminPassword }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return loginBody.GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> GetDashboardMetricsAsync(HttpClient client, string platformToken, CancellationToken cancellationToken)
    {
        var response = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, platformToken, "/api/platform/dashboard", cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private async Task ForceDrainTenancyOutboxAsync(CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var outboxProcessor = scope.ServiceProvider.GetRequiredService<OutboxProcessor<TenancyDbContext>>();
        await outboxProcessor.ProcessPendingMessagesAsync(cancellationToken);
    }

    // A Subscription nasce via consumidor de RabbitMQ, nao numa chamada
    // sincrona da criacao do tenant — mesmo padrao de poll de BillingTests.
    private async Task<Subscription> WaitForSubscriptionAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // 20 tentativas (5s) bastam isolado, mas a suite completa acumula
        // backlog no consumidor de RabbitMQ com centenas de tenants criados
        // por outros testes — janela maior evita flakiness sem mudar o
        // comportamento de producao (so a paciencia do teste).
        Subscription? subscription = null;
        for (var attempt = 0; attempt < 60 && subscription is null; attempt++)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            subscription = await dbContext.Subscriptions.AsNoTracking().SingleOrDefaultAsync(
                s => s.TenantId == TenantId.From(tenantId), cancellationToken);

            if (subscription is null)
            {
                await Task.Delay(250, cancellationToken);
            }
        }

        subscription.ShouldNotBeNull();
        return subscription;
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
        await client.PostAsJsonAsync(
            "/api/auth/register", new { tenantId, email = ownerEmail, password = "SenhaForte123!", fullName = "Dono" }, cancellationToken);
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        return (tenantId, ownerEmail);
    }

    private async Task<string> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var (tenantId, ownerEmail) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = "SenhaForte123!" }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return loginBody.GetProperty("accessToken").GetString()!;
    }
}
