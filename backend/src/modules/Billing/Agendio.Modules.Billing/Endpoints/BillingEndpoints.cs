using Agendio.Infrastructure.Endpoints;
using Agendio.Modules.Billing.Application.CancelSubscription;
using Agendio.Modules.Billing.Application.GetMySubscription;
using Agendio.Modules.Billing.Application.ListPlans;
using Agendio.Modules.Billing.Application.ProcessAsaasWebhook;
using Agendio.Modules.Billing.Application.SubscribeToPlan;
using Agendio.Modules.Billing.Infrastructure.Asaas;
using Agendio.SharedKernel.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

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

        group.MapPost("/subscription/cancel", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.Send(new CancelSubscriptionCommand(), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : result.Error.ToProblemResult();
        })
        .WithName("CancelSubscription")
        .WithSummary("Cancela a assinatura do estabelecimento atual.");

        endpoints.MapPost("/api/webhooks/asaas", async (
            HttpRequest httpRequest, AsaasWebhookPayload payload, IOptions<AsaasOptions> asaasOptions,
            IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var receivedToken = httpRequest.Headers["asaas-access-token"].ToString();
            if (!string.Equals(receivedToken, asaasOptions.Value.WebhookSecret, StringComparison.Ordinal))
            {
                return Results.Unauthorized();
            }

            var command = new ProcessAsaasWebhookCommand(
                payload.Event, payload.Payment.Id, payload.Payment.Subscription,
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

    private sealed record AsaasWebhookPayload(string Event, AsaasWebhookPayment Payment);

    private sealed record AsaasWebhookPayment(string Id, string Status, decimal Value, DateOnly DueDate, string? InvoiceUrl, string BillingType, string? Subscription);
}
