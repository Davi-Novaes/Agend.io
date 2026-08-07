using Agendio.Modules.Billing.Contracts;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Billing.Infrastructure;

internal sealed class BillingAdministrationService(BillingDbContext dbContext) : IBillingAdministrationService
{
    public async Task<IReadOnlyList<SubscriptionSummary>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var subscriptions = await (
            from s in dbContext.Subscriptions.AsNoTracking()
            join p in dbContext.Plans.AsNoTracking() on s.PlanId equals p.Id
            select new SubscriptionSummary(s.TenantId.Value, p.Name, s.Status.ToString(), s.TrialEndsAtUtc, s.CurrentPeriodEndsAtUtc))
            .ToListAsync(cancellationToken);

        return subscriptions;
    }
}
