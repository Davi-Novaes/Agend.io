using Agendio.Modules.Billing.Contracts;
using Agendio.Modules.Billing.Infrastructure.Asaas;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Billing.Infrastructure;

internal sealed class BillingAdministrationService(BillingDbContext dbContext, IAsaasClient asaasClient, IClock clock)
    : IBillingAdministrationService
{
    public async Task<IReadOnlyList<SubscriptionSummary>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var subscriptions = await (
            from s in dbContext.Subscriptions.AsNoTracking()
            join p in dbContext.Plans.AsNoTracking() on s.PlanId equals p.Id
            select new SubscriptionSummary(
                s.TenantId.Value, p.Name, p.PriceAmount, p.Currency, s.Status.ToString(), s.TrialEndsAtUtc, s.CurrentPeriodEndsAtUtc))
            .ToListAsync(cancellationToken);

        return subscriptions;
    }

    public async Task<Result> CancelSubscriptionForTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var subscription = await dbContext.Subscriptions.SingleOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (subscription is null)
        {
            return Result.Failure(Error.NotFound("Subscription.NotFound", "Assinatura nao encontrada."));
        }

        var cancelResult = subscription.Cancel(clock.UtcNow);
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        if (subscription.AsaasSubscriptionId is not null)
        {
            await asaasClient.CancelSubscriptionAsync(subscription.AsaasSubscriptionId, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
