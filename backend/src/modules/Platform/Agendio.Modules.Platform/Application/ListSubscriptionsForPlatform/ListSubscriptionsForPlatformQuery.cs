using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Platform.Application.ListSubscriptionsForPlatform;

public sealed record ListSubscriptionsForPlatformQuery : IQuery<IReadOnlyList<SubscriptionAdminSummary>>;

public sealed record SubscriptionAdminSummary(
    Guid TenantId, string TenantName, string PlanName, string Status, DateTimeOffset TrialEndsAtUtc, DateTimeOffset? CurrentPeriodEndsAtUtc);
