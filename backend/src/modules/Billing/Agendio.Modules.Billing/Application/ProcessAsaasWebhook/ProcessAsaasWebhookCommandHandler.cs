using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Agendio.Modules.Billing.Application.ProcessAsaasWebhook;

/// <summary>
/// Idempotente por design: upsert de Payment por AsaasPaymentId (indice unico)
/// — reentrega do mesmo evento pela Asaas (ela reenvia em caso de timeout/nao-2xx)
/// nunca duplica uma linha nem processa duas vezes o mesmo pagamento.
///
/// O PRIMEIRO Payment de uma assinatura paga durante o cadastro (Subscribe
/// classico) nasce em SubscribeToPlanCommandHandler; cobrancas dos ciclos
/// seguintes (mes 2, 3...) a Asaas gera sozinha. Ja o Checkout do onboarding
/// (Fase 24) nunca chama POST /subscriptions diretamente — a Asaas cria a
/// assinatura por conta propria quando o pagador confirma o cartao na pagina
/// hospedada, entao o PRIMEIRO webhook dessa assinatura chega com um
/// AsaasSubscriptionId que a Subscription local ainda nao conhece. Nesse caso
/// o handler correlaciona via ExternalReference (o TenantId, setado na hora
/// de criar o Checkout) e adota o AsaasSubscriptionId na Subscription certa —
/// sem precisar de um evento de webhook dedicado pra "checkout concluido".
/// </summary>
public sealed class ProcessAsaasWebhookCommandHandler(BillingDbContext dbContext, IClock clock, ILogger<ProcessAsaasWebhookCommandHandler> logger)
    : ICommandHandler<ProcessAsaasWebhookCommand>
{
    public async Task<Result> Handle(ProcessAsaasWebhookCommand request, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments.SingleOrDefaultAsync(p => p.AsaasPaymentId == request.AsaasPaymentId, cancellationToken);

        if (payment is null)
        {
            if (string.IsNullOrEmpty(request.AsaasSubscriptionId))
            {
                // Cobranca avulsa fora de uma assinatura — nao e algo que este modulo acompanha.
                return Result.Success();
            }

            var subscriptionForNewPayment = await dbContext.Subscriptions
                .SingleOrDefaultAsync(s => s.AsaasSubscriptionId == request.AsaasSubscriptionId, cancellationToken);

            subscriptionForNewPayment ??= await TryAdoptCheckoutOriginatedSubscriptionAsync(request, cancellationToken);

            if (subscriptionForNewPayment is null)
            {
                logger.LogWarning(
                    "Webhook para AsaasSubscriptionId {AsaasSubscriptionId} sem Subscription local correspondente — ignorado.",
                    request.AsaasSubscriptionId);
                return Result.Success();
            }

            payment = new Payment(
                subscriptionForNewPayment.TenantId, subscriptionForNewPayment.Id, request.AsaasPaymentId,
                request.Value, request.DueDate, request.InvoiceUrl, request.BillingType);
            dbContext.Payments.Add(payment);
        }
        else
        {
            payment.UpdateFromWebhook(request.InvoiceUrl, request.BillingType);
        }

        var subscription = await dbContext.Subscriptions.SingleOrDefaultAsync(s => s.Id == payment.SubscriptionId, cancellationToken);

        switch (request.Event)
        {
            case "PAYMENT_CONFIRMED" or "PAYMENT_RECEIVED":
                payment.MarkConfirmed(clock.UtcNow);
                // Aproximacao deliberada: proximo vencimento = vencimento
                // deste pagamento + 1 ciclo (so Monthly no MVP). A Asaas e a
                // fonte de verdade real do calendario de cobranca — isto so
                // alimenta a rede de seguranca do job de conciliacao.
                subscription?.MarkActive(new DateTimeOffset(request.DueDate.AddMonths(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
                break;
            case "PAYMENT_OVERDUE":
                payment.MarkOverdue();
                subscription?.MarkPastDue();
                break;
            case "PAYMENT_DELETED" or "PAYMENT_REFUNDED":
                payment.MarkRefunded();
                break;
            default:
                // Evento que a Asaas manda mas nao alteramos estado nenhum por
                // ele (ex.: SUBSCRIPTION_CREATED) — no-op deliberado.
                break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// So encontra algo quando AsaasSubscriptionId nao bateu com nenhuma
    /// Subscription local — ou seja, e o primeiro webhook de uma assinatura
    /// originada no Checkout do onboarding. Busca por TenantId (via
    /// ExternalReference) restrito a AsaasSubscriptionId == null pra nunca
    /// roubar/sobrescrever a assinatura de um tenant que ja tem uma vinculada.
    /// </summary>
    private async Task<Subscription?> TryAdoptCheckoutOriginatedSubscriptionAsync(
        ProcessAsaasWebhookCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.ExternalReference) || !Guid.TryParse(request.ExternalReference, out var tenantIdValue))
        {
            return null;
        }

        var subscription = await dbContext.Subscriptions.SingleOrDefaultAsync(
            s => s.TenantId == TenantId.From(tenantIdValue) && s.AsaasSubscriptionId == null, cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        var attachResult = subscription.AttachAsaasCheckout(request.AsaasCustomerId ?? string.Empty, request.AsaasSubscriptionId!);
        return attachResult.IsSuccess ? subscription : null;
    }
}
