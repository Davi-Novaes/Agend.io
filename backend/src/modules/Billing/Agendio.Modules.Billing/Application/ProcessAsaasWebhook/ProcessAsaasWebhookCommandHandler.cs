using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
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
/// So o PRIMEIRO Payment de uma assinatura nasce em SubscribeToPlanCommandHandler
/// (proativamente, pra ja existir invoiceUrl na hora de assinar). Cobrancas dos
/// ciclos seguintes (mes 2, 3...) a Asaas gera sozinha — a gente nunca viu esse
/// AsaasPaymentId antes, entao o handler CRIA a linha aqui, resolvendo a
/// Subscription pelo AsaasSubscriptionId que vem no payload.
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
}
