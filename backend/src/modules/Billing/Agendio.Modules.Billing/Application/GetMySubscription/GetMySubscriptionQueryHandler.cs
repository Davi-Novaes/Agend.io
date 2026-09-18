using Agendio.Modules.Billing.Infrastructure;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Billing.Application.GetMySubscription;

/// <summary>
/// Subscription/Payment nao sao ITenantOwned (ver Subscription.cs) — o filtro
/// de tenant aqui e MANUAL, nao um Global Query Filter automatico.
///
/// Auto-heal: se por qualquer motivo o consumidor de evento
/// (BillingIntegrationEventConsumer) nunca criou a Subscription deste tenant
/// (RabbitMQ fora do ar, mensagem perdida), a primeira visita a esta tela cria
/// agora — rede de seguranca contra perda de evento, ver plano do Sprint 7.
/// </summary>
public sealed class GetMySubscriptionQueryHandler(BillingDbContext dbContext, ITenantContext tenantContext, IClock clock)
    : IQueryHandler<GetMySubscriptionQuery, MySubscriptionResult>
{
    private const int InvoiceVisibilityWindowDays = 10;

    public async Task<Result<MySubscriptionResult>> Handle(GetMySubscriptionQuery request, CancellationToken cancellationToken)
    {
        var subscription = await SubscriptionProvisioning.FindOrCreateAsync(dbContext, tenantContext.TenantId, clock, cancellationToken);

        var plan = await dbContext.Plans.AsNoTracking()
            .SingleAsync(p => p.Id == subscription.PlanId, cancellationToken);

        var latestPayment = await dbContext.Payments.AsNoTracking()
            .Where(p => p.SubscriptionId == subscription.Id)
            .OrderByDescending(p => p.DueDate)
            .Select(p => new LatestPaymentSummary(p.Status.ToString(), p.Amount, p.DueDate, p.InvoiceUrl))
            .FirstOrDefaultAsync(cancellationToken);

        // A fatura so pode ser visualizada a partir de 10 dias antes do
        // vencimento (pedido do usuario) -- decisao de negocio fica aqui, no
        // handler, pra nao depender do frontend aplicar a mesma regra
        // corretamente (e pra nunca vazar a InvoiceUrl da Asaas fora da
        // janela). Fora da janela, o link some (InvoiceUrl null) mas o resto
        // do resumo (status, valor, vencimento) continua visivel normalmente.
        if (latestPayment is { InvoiceUrl: not null } payment)
        {
            var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
            if (payment.DueDate > today.AddDays(InvoiceVisibilityWindowDays))
            {
                latestPayment = payment with { InvoiceUrl = null };
            }
        }

        return Result.Success(new MySubscriptionResult(
            plan.Id.Value, plan.Name, subscription.Status.ToString(), subscription.TrialEndsAtUtc,
            subscription.CurrentPeriodEndsAtUtc, subscription.CanceledAtUtc, latestPayment));
    }
}
