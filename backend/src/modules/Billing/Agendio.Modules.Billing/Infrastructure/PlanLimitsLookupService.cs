using Agendio.Modules.Billing.Contracts;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Billing.Infrastructure;

internal sealed class PlanLimitsLookupService(BillingDbContext dbContext) : IPlanLimitsLookupService
{
    public async Task<PlanLimits?> GetActivePlanLimitsAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var subscription = await dbContext.Subscriptions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        var plan = await dbContext.Plans
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == subscription.PlanId, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        return new PlanLimits(plan.MaxUnits, plan.MaxProfessionals, plan.MaxCustomers);
    }
}
