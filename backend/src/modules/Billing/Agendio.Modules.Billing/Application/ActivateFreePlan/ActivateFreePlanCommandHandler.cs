using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.Modules.Billing.Infrastructure.Persistence.Configurations;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;

namespace Agendio.Modules.Billing.Application.ActivateFreePlan;

public sealed class ActivateFreePlanCommandHandler(BillingDbContext dbContext, ITenantContext tenantContext, IClock clock)
    : ICommandHandler<ActivateFreePlanCommand>
{
    public async Task<Result> Handle(ActivateFreePlanCommand request, CancellationToken cancellationToken)
    {
        var subscription = await SubscriptionProvisioning.FindOrCreateAsync(dbContext, tenantContext.TenantId, clock, cancellationToken);

        var activateResult = subscription.ActivateAsFree(PlanConfiguration.FreePlanId);
        if (activateResult.IsFailure)
        {
            return activateResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
