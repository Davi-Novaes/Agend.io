using Agendio.Modules.Billing.Infrastructure.Asaas;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Billing.Application.CancelSubscription;

public sealed class CancelSubscriptionCommandHandler(
    BillingDbContext dbContext, ITenantContext tenantContext, IClock clock, IAsaasClient asaasClient)
    : ICommandHandler<CancelSubscriptionCommand>
{
    public async Task<Result> Handle(CancelSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.Subscriptions
            .SingleOrDefaultAsync(s => s.TenantId == tenantContext.TenantId, cancellationToken);
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
