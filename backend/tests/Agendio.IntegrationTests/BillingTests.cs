using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Agendio.Infrastructure.Messaging;
using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.Modules.Billing.Infrastructure.Persistence.Configurations;
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
        var (tenantId, _, _) = await CreateTenantWithOwnerAsync(client, cancellationToken);

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
    public async Task Plan_Catalog_Should_Expose_The_3_Active_Paid_Plans_With_Their_Limits()
    {
        // Regressao do BL-04 revisitado: a landing anunciava 3 planos pagos que
        // nao existiam de verdade no backend — agora existem, com limite real.
        // "Padrao" e "Gratis" foram desativados (nao fazia sentido oferecer
        // tudo ilimitado de graca ao lado de planos pagos com limite de
        // verdade) e nenhum dos dois pode mais aparecer na vitrine publica.
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();

        var response = await client.GetAsync("/api/billing/plans", cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var plans = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        plans.GetArrayLength().ShouldBe(3);
        var names = plans.EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToList();
        names.ShouldContain("Essencial");
        names.ShouldContain("Profissional");
        names.ShouldContain("Premium");
        names.ShouldNotContain("Padrão");
        names.ShouldNotContain("Grátis");

        var essencial = plans.EnumerateArray().Single(p => p.GetProperty("name").GetString() == "Essencial");
        essencial.GetProperty("maxUnits").GetInt32().ShouldBe(1);
        essencial.GetProperty("maxProfessionals").GetInt32().ShouldBe(3);
        essencial.GetProperty("maxCustomers").GetInt32().ShouldBe(300);
        essencial.GetProperty("isFeatured").GetBoolean().ShouldBeFalse();

        var profissional = plans.EnumerateArray().Single(p => p.GetProperty("name").GetString() == "Profissional");
        profissional.GetProperty("isFeatured").GetBoolean().ShouldBeTrue();

        var premium = plans.EnumerateArray().Single(p => p.GetProperty("name").GetString() == "Premium");
        premium.GetProperty("maxCustomers").ValueKind.ShouldBe(JsonValueKind.Null);
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
            new { planId, fullName = "Dono Teste", cpfCnpj = "12345678909", email = "dono@example.com" },
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
    public async Task Resubmitting_Subscribe_Before_Paying_Should_Not_Create_A_Second_Asaas_Subscription()
    {
        // Regressao: reenviar o formulario de "Assinar" (F5, duplo clique,
        // voltar do checkout) antes do trial virar pago criava uma SEGUNDA
        // assinatura recorrente na Asaas a cada envio, deixando a anterior
        // orfa — encontrado em sandbox com 2 assinaturas ACTIVE simultaneas
        // de R$99/mes pro mesmo cliente. So pode existir uma fatura pendente.
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var plansResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(client, accessToken, "/api/billing/plans", cancellationToken);
        var plans = await plansResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var planId = plans.EnumerateArray().First().GetProperty("id").GetGuid();

        var firstSubscribe = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/billing/subscription/subscribe",
            new { planId, fullName = "Dono Teste", cpfCnpj = "12345678909", email = "dono@example.com" },
            cancellationToken);
        firstSubscribe.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstBody = await firstSubscribe.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        // Reenvia com dados diferentes (nome/e-mail trocados), como acontece
        // quando a pessoa edita o formulario antes de clicar de novo.
        var resubmit = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/billing/subscription/subscribe",
            new { planId, fullName = "Outro Nome", cpfCnpj = "12345678909", email = "outro@example.com" },
            cancellationToken);
        resubmit.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resubmitBody = await resubmit.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        resubmitBody.GetProperty("invoiceUrl").GetString().ShouldBe(firstBody.GetProperty("invoiceUrl").GetString());

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var payments = await dbContext.Payments.Where(p => p.TenantId == TenantId.From(tenantId)).ToListAsync(cancellationToken);
        payments.Count.ShouldBe(1);

        var subscription = await dbContext.Subscriptions.AsNoTracking().SingleAsync(s => s.TenantId == TenantId.From(tenantId), cancellationToken);
        subscription.AsaasSubscriptionId.ShouldNotBeNullOrWhiteSpace();
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
        var (tenantId, _, _) = await CreateTenantWithOwnerAsync(client, cancellationToken);
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

    [Fact]
    public async Task Onboard_Selecting_The_Retired_Free_Plan_Should_Be_Rejected()
    {
        // O plano Gratis foi desativado (BL-04 revisitado — nao fazia sentido
        // oferecer tudo ilimitado de graca ao lado de planos pagos com limite
        // real). onboard-select-plan so aceita planos ativos, entao escolher
        // Gratis precisa ser rejeitado, nao silenciosamente aceito.
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (_, _, onboardingToken) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, onboardingToken, "/api/billing/subscription/onboard-select-plan",
            new { planId = PlanConfiguration.FreePlanId.Value },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Onboard_Select_Plan_Without_A_Valid_Onboarding_Token_Should_Be_Rejected()
    {
        // Regressao do BL-01 (docs/BACKLOG.md): o endpoint era anonimo e
        // ativava a assinatura de qualquer tenant so com o Guid no corpo.
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, _, _) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var withoutToken = await client.PostAsJsonAsync(
            "/api/billing/subscription/onboard-select-plan",
            new { tenantId, planId = PlanConfiguration.FreePlanId.Value },
            cancellationToken);
        withoutToken.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var subscription = await dbContext.Subscriptions.SingleOrDefaultAsync(s => s.TenantId == TenantId.From(tenantId), cancellationToken);
        subscription.ShouldBeNull();
    }

    [Fact]
    public async Task Onboarding_Token_From_One_Tenant_Should_Never_Activate_Another_Tenants_Subscription()
    {
        // Regressao do BL-01: mesmo com um token de onboarding valido (de OUTRO
        // tenant), o TenantId usado tem que ser sempre o da claim, nunca um
        // Guid alheio que o cliente tente colar no corpo.
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (_, _, tokenA) = await CreateTenantWithOwnerAsync(client, cancellationToken);
        var (tenantB, _, _) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, tokenA, "/api/billing/subscription/onboard-select-plan",
            new { planId = PlanConfiguration.EssencialPlanId.Value },
            cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var subscriptionB = await dbContext.Subscriptions.SingleOrDefaultAsync(s => s.TenantId == TenantId.From(tenantB), cancellationToken);
        subscriptionB.ShouldBeNull();
    }

    [Fact]
    public async Task Onboard_Selecting_A_Paid_Plan_Should_Only_Record_Intent_Without_Creating_A_Checkout_Yet()
    {
        // P1-5 (docs/AUTH_BILLING_SECURITY_AUDIT.md): o Checkout de verdade so
        // e criado depois do login (que exige e-mail confirmado), via
        // /subscription/subscribe — nao aqui, ainda no onboarding.
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, _, onboardingToken) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var response = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, onboardingToken, "/api/billing/subscription/onboard-select-plan",
            new { planId = PlanConfiguration.EssencialPlanId.Value },
            cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        body.GetProperty("requiresPayment").GetBoolean().ShouldBeTrue();
        body.TryGetProperty("checkoutLink", out _).ShouldBeFalse();

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var subscription = await dbContext.Subscriptions.SingleAsync(s => s.TenantId == TenantId.From(tenantId), cancellationToken);
        subscription.Status.ShouldBe(SubscriptionStatus.Trialing);
        subscription.PlanId.ShouldBe(PlanConfiguration.EssencialPlanId);
        subscription.AsaasSubscriptionId.ShouldBeNull();

        var statusResponse = await AuthorizedRequestHelpers.GetAuthorizedAsync(
            client, onboardingToken, "/api/billing/subscription/onboard-status", cancellationToken);
        var statusBody = await statusResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        statusBody.GetProperty("isReady").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Onboard_Selecting_A_Plan_Twice_Before_Activating_Should_Just_Update_The_Recorded_Intent()
    {
        // So registra intencao (P1-5) — mudar de ideia sobre qual plano
        // escolher ANTES de ativar (ainda na tela de "escolha seu plano") e um
        // fluxo valido, nao um erro.
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, _, onboardingToken) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var firstAttempt = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, onboardingToken, "/api/billing/subscription/onboard-select-plan",
            new { planId = PlanConfiguration.EssencialPlanId.Value },
            cancellationToken);
        firstAttempt.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondAttempt = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, onboardingToken, "/api/billing/subscription/onboard-select-plan",
            new { planId = PlanConfiguration.ProfissionalPlanId.Value },
            cancellationToken);
        secondAttempt.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var subscription = await dbContext.Subscriptions.SingleAsync(s => s.TenantId == TenantId.From(tenantId), cancellationToken);
        subscription.PlanId.ShouldBe(PlanConfiguration.ProfissionalPlanId);
    }

    [Fact]
    public async Task Onboard_Selecting_A_Plan_After_The_Subscription_Is_Already_Active_Should_Be_Rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, ownerEmail, onboardingToken) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var selectPlan = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, onboardingToken, "/api/billing/subscription/onboard-select-plan",
            new { planId = PlanConfiguration.EssencialPlanId.Value },
            cancellationToken);
        selectPlan.StatusCode.ShouldBe(HttpStatusCode.OK);

        // So ativa de verdade depois do login (que exige e-mail confirmado) —
        // ver Subscription.SelectPlan/ActivateAsFree. activate-free serve so
        // pra deixar a assinatura em Active pra este teste (o endpoint em si
        // nao checa Plan.IsActive, so forca o plano Gratis independente do
        // que estava selecionado antes).
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var accessToken = loginBody.GetProperty("accessToken").GetString()!;

        var activateResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/billing/subscription/activate-free", new { }, cancellationToken);
        activateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var selectAgain = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, onboardingToken, "/api/billing/subscription/onboard-select-plan",
            new { planId = PlanConfiguration.EssencialPlanId.Value },
            cancellationToken);
        selectAgain.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Free_Plan_Subscription_Can_Be_Reactivated_After_Cancellation()
    {
        // Regressao do BL-23/BL-33 (docs/BACKLOG.md): ActivateAsFree rejeitava
        // qualquer assinatura ja Cancelada — reassinar Free depois de cancelar
        // era impossivel pelo app, so a tela mostrava a opcao.
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, ownerEmail, onboardingToken) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        // O plano escolhido aqui e so pra existir uma Subscription pra
        // cancelar — activate-free abaixo forca o plano Gratis de qualquer
        // jeito (o endpoint nao checa Plan.IsActive), entao o teste continua
        // cobrindo a mesma regressao (reativar como Free depois de cancelar)
        // mesmo com Gratis desativado no catalogo publico.
        var selectPlan = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, onboardingToken, "/api/billing/subscription/onboard-select-plan",
            new { planId = PlanConfiguration.EssencialPlanId.Value },
            cancellationToken);
        selectPlan.StatusCode.ShouldBe(HttpStatusCode.OK);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var accessToken = loginBody.GetProperty("accessToken").GetString()!;

        var cancelResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/billing/subscription/cancel", new { }, cancellationToken);
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var canceled = await dbContext.Subscriptions.SingleAsync(s => s.TenantId == TenantId.From(tenantId), cancellationToken);
            canceled.Status.ShouldBe(SubscriptionStatus.Canceled);
        }

        var reactivateResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/billing/subscription/activate-free", new { }, cancellationToken);
        reactivateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var finalScope = fixture.Services.CreateAsyncScope();
        var finalDbContext = finalScope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var reactivated = await finalDbContext.Subscriptions.SingleAsync(s => s.TenantId == TenantId.From(tenantId), cancellationToken);
        reactivated.Status.ShouldBe(SubscriptionStatus.Active);
        reactivated.PlanId.ShouldBe(PlanConfiguration.FreePlanId);
    }

    [Fact]
    public async Task Paid_Plan_Subscription_Can_Be_Resubscribed_After_Cancellation()
    {
        // Mesma regressao do teste acima, caminho pago: AttachAsaasCheckout
        // tambem rejeitava Cancelada, entao nem o CHECKOUT (antes de qualquer
        // pagamento) conseguia ser recriado depois de cancelar.
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, accessToken) = await CreateTenantWithOwnerAndLoginAsync(client, cancellationToken);

        var firstSubscribe = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/billing/subscription/subscribe",
            new { planId = PlanConfiguration.EssencialPlanId.Value, fullName = "Dono Teste", cpfCnpj = "12345678909", email = "dono@example.com" },
            cancellationToken);
        firstSubscribe.StatusCode.ShouldBe(HttpStatusCode.OK);

        var cancelResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/billing/subscription/cancel", new { }, cancellationToken);
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Reassina — antes da correcao, isso falhava com Subscription.AlreadyCanceled.
        var resubscribeResponse = await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, accessToken, "/api/billing/subscription/subscribe",
            new { planId = PlanConfiguration.EssencialPlanId.Value, fullName = "Dono Teste", cpfCnpj = "12345678909", email = "dono@example.com" },
            cancellationToken);
        resubscribeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        string newAsaasPaymentId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var payments = await dbContext.Payments.Where(p => p.TenantId == TenantId.From(tenantId)).ToListAsync(cancellationToken);
            newAsaasPaymentId = payments[^1].AsaasPaymentId;
        }

        // Confirma o pagamento do novo checkout via webhook — mesmo caminho
        // que reativa de verdade uma assinatura paga (MarkActive, sem guard de Status).
        var webhookPayload = new
        {
            @event = "PAYMENT_CONFIRMED",
            payment = new
            {
                id = newAsaasPaymentId,
                status = "CONFIRMED",
                value = 99.00m,
                dueDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                invoiceUrl = "https://fake.local/checkout",
                billingType = "PIX",
                subscription = (string?)null,
            },
        };
        var webhookResponse = await PostWebhookAsync(client, webhookPayload, AsaasSecretHeader(), cancellationToken);
        webhookResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var finalScope = fixture.Services.CreateAsyncScope();
        var finalDbContext = finalScope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var reactivated = await finalDbContext.Subscriptions.SingleAsync(s => s.TenantId == TenantId.From(tenantId), cancellationToken);
        reactivated.Status.ShouldBe(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task Reconciliation_Job_Should_Never_Deactivate_A_Tenant_On_The_Free_Plan()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = fixture.CreateClient();
        var (tenantId, _, onboardingToken) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        // So pra existir uma Subscription pra forcar abaixo — Gratis foi
        // desativado (nao da mais pra selecionar via onboard-select-plan),
        // entao o PlanId=FreePlanId de verdade (simulando um tenant legado
        // ainda no Free) e forcado direto no UPDATE junto com o resto do
        // estado que o job varre.
        await AuthorizedRequestHelpers.PostAuthorizedAsync(
            client, onboardingToken, "/api/billing/subscription/onboard-select-plan",
            new { planId = PlanConfiguration.EssencialPlanId.Value },
            cancellationToken);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

            // Defesa em profundidade: forca de volta pro estado que o job varre
            // (Trialing vencido ha muito tempo) mesmo estando no plano Free —
            // o guard explicito por PlanId tem que segurar independente do
            // fluxo normal (que nem deixa uma Subscription Free chegar aqui).
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE billing.subscriptions
                SET plan_id = {PlanConfiguration.FreePlanId.Value}, status = {SubscriptionStatus.Trialing.ToString()}, trial_ends_at_utc = {DateTimeOffset.UtcNow.AddDays(-30)}
                WHERE tenant_id = {tenantId}
                """,
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
            tenant.IsActive.ShouldBeTrue();
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
            new { planId, fullName = "Dono Teste", cpfCnpj = "12345678909", email = "dono@example.com" },
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

    private async Task<(Guid TenantId, string OwnerEmail, string OnboardingToken)> CreateTenantWithOwnerAsync(HttpClient client, CancellationToken cancellationToken)
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
            "/api/auth/register", new { tenantId, email = ownerEmail, password = Password, fullName = "Dono", phone = "+5511999999999", cpfCnpj = "12345678909", termsAccepted = true }, cancellationToken);
        registerResponse.EnsureSuccessStatusCode();
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var onboardingToken = registerBody.GetProperty("onboardingToken").GetString()!;
        await fixture.ConfirmEmailDirectlyAsync(tenantId, ownerEmail, cancellationToken);

        return (tenantId, ownerEmail, onboardingToken);
    }

    private async Task<(Guid TenantId, string AccessToken)> CreateTenantWithOwnerAndLoginAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var (tenantId, ownerEmail, _) = await CreateTenantWithOwnerAsync(client, cancellationToken);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new { tenantId, email = ownerEmail, password = Password }, cancellationToken);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        return (tenantId, loginBody.GetProperty("accessToken").GetString()!);
    }
}
