namespace Agendio.Modules.Billing.Contracts;

public sealed record SubscriptionSummary(
    Guid TenantId,
    string PlanName,
    decimal PlanPriceAmount,
    string PlanCurrency,
    string Status,
    DateTimeOffset TrialEndsAtUtc,
    DateTimeOffset? CurrentPeriodEndsAtUtc);
