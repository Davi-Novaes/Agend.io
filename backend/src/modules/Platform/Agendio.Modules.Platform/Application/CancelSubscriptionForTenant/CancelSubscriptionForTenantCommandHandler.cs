using Agendio.Modules.Billing.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Platform.Application.CancelSubscriptionForTenant;

public sealed class CancelSubscriptionForTenantCommandHandler(IBillingAdministrationService billingAdministrationService)
    : ICommandHandler<CancelSubscriptionForTenantCommand>
{
    public Task<Result> Handle(CancelSubscriptionForTenantCommand request, CancellationToken cancellationToken) =>
        billingAdministrationService.CancelSubscriptionForTenantAsync(TenantId.From(request.TenantId), cancellationToken);
}
