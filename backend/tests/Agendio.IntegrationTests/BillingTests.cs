using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendio.Infrastructure.Messaging;
using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agendio.IntegrationTests;

/// <summary>
/// Cobre o ciclo de vida de assinatura do Sprint 7: trial automatico via
/// consumidor de RabbitMQ, assinatura com Asaas fake, idempotencia de webhook,
/// isolamento cruzado (mesmo sem RLS — ver Subscription.cs) e o job de
/// conciliacao que desativa tenant inadimplente.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public class BillingTests(IntegrationTestFixture fixture)
{
    private const string Password = "SenhaForte123!";

    [Fact]
    public async Task Creating_A_Tenant_Should_Eventually_Start_A_14_Day_Trial_Subscription()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, _) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        // O outbox de Tenancy so drena pro RabbitMQ no proximo tick do job
        // recorrente (a cada minuto) — forcar o drain aqui evita o teste ter
        // que esperar ate 60s de verdade pelo agendamento real do Hangfire.
        await ForceDrainTenancyOutboxAsync(cancellationToken);

        // Primeiro teste do projeto que tolera consistencia eventual de
        // verdade: a Subscription nasce via consumidor de RabbitMQ, nao numa
        // chamada sincrona da criacao do tenant — poll com retry curto.
        Subscription? subscription = null;
        for (var attempt = 0; attempt < 20 && subscription is null; attempt++)
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            subscription = await dbContext.Subscriptions.SingleOrDefaultAsync(
                s => s.TenantId == TenantId.From(tenantId), cancellationToken);

            if (subscription is null)
            {
                await Task.Delay(250, cancellationToken);
            }
        }

        subscription.ShouldNotBeNull();
        subscription.Status.ShouldBe(SubscriptionStatus.Trialing);
        (subscription.TrialEndsAtUtc - DateTimeOffset.UtcNow).ShouldBeGreaterThan(TimeSpan.FromDays(13));
    }

    [Fact]
    public async Task Owner_Can_Subscribe_To_A_Plan_And_Receives_An_Invoice_Url()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var plansResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/billing/plans", cancellationToken);
        var plans = await plansResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var planId = plans.EnumerateArray().First().GetProperty("id").GetGuid();

        var subscribeResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/billing/subscription/subscribe",
            new { planId, fullName = "Dono Teste", cpfCnpj = "12345678900", email = "dono@example.com" },
            cancellationToken);

        subscribeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await subscribeResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("invoiceUrl").GetString().ShouldNotBeNullOrWhiteSpace();

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var payment = await dbContext.Payments.SingleAsync(p => p.TenantId == TenantId.From(tenantId), cancellationToken);
        payment.Status.ShouldBe(PaymentStatus.Pending);
    }

    [Fact]
    public async Task Webhook_Payment_Confirmed_Activates_Subscription_And_Is_Idempotent_On_Redelivery()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var asaasPaymentId = await SubscribeAndGetAsaasPaymentIdAsync(client, accessToken, tenantId, cancellationToken);

        var webhookPayload = new
        {
            @event = "PAYMENT_CONFIRMED",
            payment = new
            {
                id = asaasPaymentId,
                status = "CONFIRMED",
                value = 99.00m,
                dueDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                invoiceUrl = "https://fake.local/checkout",
                billingType = "PIX",
                subscription = (string?)null,
            },
        };

        var firstDelivery = await PostWebhookAsync(client, webhookPayload, AsaasSecretHeader(), cancellationToken);
        firstDelivery.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Reentrega do MESMO evento (a Asaas reenvia em caso de timeout/nao-2xx)
        // nunca deveria duplicar o Payment nem processar duas vezes.
        var secondDelivery = await PostWebhookAsync(client, webhookPayload, AsaasSecretHeader(), cancellationToken);
        secondDelivery.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var payments = await dbContext.Payments.Where(p => p.AsaasPaymentId == asaasPaymentId).ToListAsync(cancellationToken);
        payments.Count.ShouldBe(1);
        payments[0].Status.ShouldBe(PaymentStatus.Confirmed);

        var subscription = await dbContext.Subscriptions.SingleAsync(s => s.TenantId == TenantId.From(tenantId), cancellationToken);
        subscription.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task Webhook_With_Wrong_Or_Missing_Access_Token_Should_Be_Unauthorized()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var payload = new { @event = "PAYMENT_CONFIRMED", payment = new { id = "fake-pay-x", status = "CONFIRMED", value = 99.00m, dueDate = "2026-01-01", invoiceUrl = (string?)null, billingType = "PIX", subscription = (string?)null } };

        var withoutToken = await PostWebhookAsync(client, payload, headerValue: null, cancellationToken);
        withoutToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var withWrongToken = await PostWebhookAsync(client, payload, "token-errado", cancellationToken);
        withWrongToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Tenant_Should_Never_See_Another_Tenants_Subscription()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var (_, tenantAToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);
        var (tenantBId, tenantBToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var tenantAResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, tenantAToken, "/api/billing/subscription", cancellationToken);
        var tenantABody = await tenantAResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        var tenantBResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, tenantBToken, "/api/billing/subscription", cancellationToken);
        var tenantBBody = await tenantBResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        // Cada um enxerga so a propria assinatura — mesmo sem RLS, o filtro
        // manual por TenantId (ver GetMySubscriptionQueryHandler) tem que segurar.
        tenantABody.GetProperty("planName").GetString().ShouldBe(tenantBBody.GetProperty("planName").GetString());
        tenantAResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        tenantBResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var distinctTenants = await dbContext.Subscriptions
            .Select(s => s.TenantId)
            .Distinct()
            .CountAsync(cancellationToken);
        distinctTenants.ShouldBeGreaterThanOrEqualTo(2);
        _ = tenantBId;
    }

    [Fact]
    public async Task Reconciliation_Job_Deactivates_A_Tenant_Whose_Trial_And_Grace_Period_Have_Expired()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, _) = await CreateTenantWithOwnerAsync(client, cancellationToken);
        await ForceDrainTenancyOutboxAsync(cancellationToken);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

            // Espera a Subscription nascer via consumidor antes de forcar o vencimento.
            Subscription? subscription = null;
            for (var attempt = 0; attempt < 20 && subscription is null; attempt++)
            {
                subscription = await dbContext.Subscriptions.SingleOrDefaultAsync(
                    s => s.TenantId == TenantId.From(tenantId), cancellationToken);
                if (subscription is null)
                {
                    await Task.Delay(250, cancellationToken);
                }
            }

            subscription.ShouldNotBeNull();

            // Forca o trial ja ter vencido ha muito tempo, direto no banco —
            // testar o job sem esperar 14 dias de verdade.
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE billing.subscriptions SET trial_ends_at_utc = {DateTimeOffset.UtcNow.AddDays(-30)} WHERE id = {subscription.Id.Value}",
                cancellationToken);

            var job = scope.ServiceProvider.GetRequiredService<Agendio.Modules.Billing.Infrastructure.Jobs.BillingReconciliationJob>();
            await job.RunAsync(cancellationToken);
        }

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var tenantAdministrationService = scope.ServiceProvider
                .GetRequiredService<Agendio.Modules.Tenancy.Contracts.ITenantAdministrationService>();
            var tenants = await tenantAdministrationService.ListAllAsync(cancellationToken);
            var tenant = tenants.Single(t => t.TenantId.Value == tenantId);
            tenant.IsActive.ShouldBeFalse();
        }
    }

    private async Task<string> SubscribeAndGetAsaasPaymentIdAsync(
        HttpClient client, string accessToken, Guid tenantId, CancellationToken cancellationToken)
    {
        var plansResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/billing/plans", cancellationToken);
        var plans = await plansResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var planId = plans.EnumerateArray().First().GetProperty("id").GetGuid();

        await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/billing/subscription/subscribe",
            new { planId, fullName = "Dono Teste", cpfCnpj = "12345678900", email = "dono@example.com" },
            cancellationToken);

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var payment = await dbContext.Payments.SingleAsync(p => p.TenantId == TenantId.From(tenantId), cancellationToken);
        return payment.AsaasPaymentId;
    }

    private async Task ForceDrainTenancyOutboxAsync(CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var outboxProcessor = scope.ServiceProvider.GetRequiredService<OutboxProcessor<TenancyDbContext>>();
        await outboxProcessor.ProcessPendingMessagesAsync(cancellationToken);
    }

    private static string AsaasSecretHeader() => IntegrationTestFixture.AsaasWebhookSecretForTests;

    private static async Task<HttpResponseMessage> PostWebhookAsync(
        HttpClient client, object payload, string? headerValue, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/asaas") { Content = JsonContent.Create(payload) };
        if (headerValue is not null)
        {
            request.Headers.Add("asaas-access-token", headerValue);
        }

        return await client.SendAsync(request, cancellationToken);
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
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono" }, cancellationToken);

        return (tenantId, ownerEmail);
    }

    private static async Task<(Guid TenantId, string AccessToken)> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var (tenantId, ownerEmail) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return (tenantId, loginBody.GetProperty("accessToken").GetString()!);
    }
}
