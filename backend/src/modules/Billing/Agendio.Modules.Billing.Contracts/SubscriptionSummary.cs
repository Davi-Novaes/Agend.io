namespace Agendio.Modules.Billing.Contracts;

public sealed record SubscriptionSummary(
    Guid TenantId,
    string PlanName,
    string Status,
    DateTimeOffset TrialEndsAtUtc,
    DateTimeOffset? CurrentPeriodEndsAtUtc);
