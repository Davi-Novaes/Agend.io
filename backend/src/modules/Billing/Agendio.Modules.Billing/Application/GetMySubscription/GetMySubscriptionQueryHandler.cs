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

        return Result.Success(new MySubscriptionResult(
            plan.Name, subscription.Status.ToString(), subscription.TrialEndsAtUtc, subscription.CurrentPeriodEndsAtUtc, latestPayment));
    }
}
