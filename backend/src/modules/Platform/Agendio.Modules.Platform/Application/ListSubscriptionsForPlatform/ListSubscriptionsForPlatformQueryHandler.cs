using Agendio.Modules.Billing.Contracts;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Platform.Application.ListSubscriptionsForPlatform;

public sealed class ListSubscriptionsForPlatformQueryHandler(
    IBillingAdministrationService billingAdministrationService, ITenantAdministrationService tenantAdministrationService)
    : IQueryHandler<ListSubscriptionsForPlatformQuery, IReadOnlyList<SubscriptionAdminSummary>>
{
    public async Task<Result<IReadOnlyList<SubscriptionAdminSummary>>> Handle(
        ListSubscriptionsForPlatformQuery request, CancellationToken cancellationToken)
    {
        var subscriptions = await billingAdministrationService.ListAllAsync(cancellationToken);
        var tenants = await tenantAdministrationService.ListAllAsync(cancellationToken);
        var tenantNamesById = tenants.ToDictionary(t => t.TenantId.Value, t => t.Name);

        IReadOnlyList<SubscriptionAdminSummary> summaries = subscriptions
            .Select(s => new SubscriptionAdminSummary(
                s.TenantId,
                tenantNamesById.GetValueOrDefault(s.TenantId, "(desconhecido)"),
                s.PlanName,
                s.Status,
                s.TrialEndsAtUtc,
                s.CurrentPeriodEndsAtUtc))
            .ToList();

        return Result.Success(summaries);
    }
}
