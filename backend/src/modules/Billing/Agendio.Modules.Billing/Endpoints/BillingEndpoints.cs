using Agendio.Infrastructure.Endpoints;
using Agendio.Infrastructure.Security;
using Agendio.Modules.Billing.Application.ActivateFreePlan;
using Agendio.Modules.Billing.Application.CancelSubscription;
using Agendio.Modules.Billing.Application.GetMySubscription;
using Agendio.Modules.Billing.Application.GetOnboardingSubscriptionStatus;
using Agendio.Modules.Billing.Application.ListPlans;
using Agendio.Modules.Billing.Application.OnboardSelectPlan;
using Agendio.Modules.Billing.Application.ProcessAsaasWebhook;
using Agendio.Modules.Billing.Application.SubscribeToPlan;
using Agendio.Modules.Billing.Infrastructure.Asaas;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Agendio.Modules.Billing.Endpoints;

public sealed class BillingEndpoints : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/billing").WithTags("Billing").RequireAuthorization();

        group.MapGet("/plans", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new ListPlansQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        // Publico de proposito: preco de plano nao e dado sensivel (toda SaaS
        // mostra isso na propria landing page) — e o onboarding precisa listar
        // os planos antes do dono ter qualquer JWT.
        .AllowAnonymous()
        .WithName("ListPlans")
        .WithSummary("Lista os planos ativos disponiveis para assinatura.");

        group.MapGet("/subscription", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Query(new GetMySubscriptionQuery(), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("GetMySubscription")
        .WithSummary("Status da assinatura do estabelecimento atual (trial, ativa, atrasada...).");

        group.MapPost("/subscription/subscribe", async (SubscribeRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var command = new SubscribeToPlanCommand(request.PlanId, request.FullName, request.CpfCnpj, request.Email);
            var result = await dispatcher.Send(command, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .WithName("SubscribeToPlan")
        .WithSummary("Assina um plano — devolve a URL de pagamento hospedada pela Asaas (PIX/boleto/cartao).");

        // Autenticado normal (nao onboarding) de proposito: cobre tanto o dono
        // que nunca escolheu Free quanto reassinar Free depois de cancelar
        // (BL-23/BL-33, docs/BACKLOG.md) — sem isso, a unica forma de "assinar
        // Free" fora do onboarding era o form de plano pago (pedia CPF/CNPJ e
        // tentava criar uma assinatura de R$0 na Asaas).
        group.MapPost("/subscription/activate-free", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new ActivateFreePlanCommand(), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("ActivateFreePlan")
        .WithSummary("Ativa o plano Free para o estabelecimento atual, sem Asaas.");

        group.MapPost("/subscription/cancel", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new CancelSubscriptionCommand(), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("CancelSubscription")
        .WithSummary("Cancela a assinatura do estabelecimento atual.");

        // Mapeados fora de "group" DE PROPOSITO (mesmo padrao do webhook da Asaas
        // abaixo): "group" carrega .RequireAuthorization() default (scheme de
        // tenant), e empilhar mais uma policy em cima exigiria os DOIS schemes
        // passarem — o de onboarding nunca vai satisfazer o de tenant. Antes,
        // estes dois endpoints eram anonimos e confiavam num TenantId so afirmado
        // no corpo/query — qualquer um podia ativar a assinatura de qualquer
        // tenant so adivinhando o Guid (BL-01, docs/BACKLOG.md). Agora exigem o
        // token de onboarding emitido no registro (prova posse do tenant), e o
        // TenantId usado e sempre o da claim validada — nunca o que o cliente manda.
        endpoints.MapPost("/api/billing/subscription/onboard-select-plan", async (
            OnboardSelectPlanRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var tenantId = Guid.Parse(user.FindFirstValue(OnboardingAuthConstants.TenantIdClaimType)!);
            var command = new OnboardSelectPlanCommand(tenantId, request.PlanId);
            var result = await dispatcher.Send(command, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization(OnboardingAuthConstants.AuthorizationPolicy)
        .WithTags("Billing")
        .WithName("OnboardSelectPlan")
        .WithSummary("Escolhe o plano no onboarding — Free ativa direto, pago devolve o link do checkout Asaas.");

        endpoints.MapGet("/api/billing/subscription/onboard-status", async (
            ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var tenantId = Guid.Parse(user.FindFirstValue(OnboardingAuthConstants.TenantIdClaimType)!);
            var result = await dispatcher.Query(new GetOnboardingSubscriptionStatusQuery(tenantId), cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblemResult();
        })
        .RequireAuthorization(OnboardingAuthConstants.AuthorizationPolicy)
        .WithTags("Billing")
        .WithName("GetOnboardingSubscriptionStatus")
        .WithSummary("Polling do onboarding: verifica se a assinatura ja esta ativa (Free ou pagamento confirmado).");

        endpoints.MapPost("/api/webhooks/asaas", async (
            HttpRequest httpRequest, AsaasWebhookPayload payload, IOptions<AsaasOptions> asaasOptions,
            IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var receivedToken = httpRequest.Headers["asaas-access-token"].ToString();
            // Comparacao em tempo constante: um webhook e uma fronteira publica anonima,
            // string.Equals vazaria timing suficiente pra um ataque de adivinhacao do segredo.
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(receivedToken),
                    Encoding.UTF8.GetBytes(asaasOptions.Value.WebhookSecret)))
            {
                return Results.Unauthorized();
            }

            var command = new ProcessAsaasWebhookCommand(
                payload.Event, payload.Payment.Id, payload.Payment.Subscription, payload.Payment.Customer, payload.Payment.ExternalReference,
                payload.Payment.Value, payload.Payment.DueDate, payload.Payment.InvoiceUrl, payload.Payment.BillingType);
            var result = await dispatcher.Send(command, cancellationToken);
            return result.IsSuccess ? Results.Ok() : result.Error.ToProblemResult();
        })
        // Publica de proposito: a Asaas nao autentica como tenant nem como
        // Platform. Verificado pelo header "asaas-access-token" acima — Asaas
        // nao assina o corpo, e um token estatico configurado no painel dela.
        .AllowAnonymous()
        .WithTags("Billing")
        .WithName("AsaasWebhook")
        .WithSummary("Recebe eventos de pagamento da Asaas (confirmacao, atraso, estorno).");
    }

    private sealed record SubscribeRequest(Guid PlanId, string FullName, string CpfCnpj, string? Email);

    private sealed record OnboardSelectPlanRequest(Guid PlanId);

    private sealed record AsaasWebhookPayload(string Event, AsaasWebhookPayment Payment);

    private sealed record AsaasWebhookPayment(
        string Id, string Status, decimal Value, DateOnly DueDate, string? InvoiceUrl, string BillingType,
        string? Subscription, string? Customer, string? ExternalReference);
}
