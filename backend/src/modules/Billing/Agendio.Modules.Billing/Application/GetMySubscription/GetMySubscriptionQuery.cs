using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Billing.Application.GetMySubscription;

public sealed record GetMySubscriptionQuery : IQuery<MySubscriptionResult>;

public sealed record MySubscriptionResult(
    string PlanName,
    string Status,
    DateTimeOffset TrialEndsAtUtc,
    DateTimeOffset? CurrentPeriodEndsAtUtc,
    LatestPaymentSummary? LatestPayment);

public sealed record LatestPaymentSummary(string Status, decimal Amount, DateOnly DueDate, string? InvoiceUrl);
