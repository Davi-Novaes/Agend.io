using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Billing.Application.GetOnboardingSubscriptionStatus;

public sealed record GetOnboardingSubscriptionStatusQuery(Guid TenantId)
    : IQuery<OnboardingSubscriptionStatusResult>, IHasExplicitTenant;

public sealed record OnboardingSubscriptionStatusResult(bool IsReady);
