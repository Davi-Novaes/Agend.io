using Agendio.Modules.Billing.Infrastructure;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;

namespace Agendio.Modules.Billing.Application.GetSubscriptionGateStatus;

public sealed class GetSubscriptionGateStatusQueryHandler(BillingDbContext dbContext, ITenantContext tenantContext, IClock clock)
    : IQueryHandler<GetSubscriptionGateStatusQuery, SubscriptionGateStatusResult>
{
    public async Task<Result<SubscriptionGateStatusResult>> Handle(GetSubscriptionGateStatusQuery request, CancellationToken cancellationToken)
    {
        var subscription = await SubscriptionProvisioning.FindOrCreateAsync(dbContext, tenantContext.TenantId, clock, cancellationToken);

        return Result.Success(new SubscriptionGateStatusResult(subscription.Status.ToString()));
    }
}
