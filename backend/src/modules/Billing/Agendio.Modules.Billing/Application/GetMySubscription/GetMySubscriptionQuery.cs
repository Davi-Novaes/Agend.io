using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Billing.Application.GetMySubscription;

public sealed record GetMySubscriptionQuery : IQuery<MySubscriptionResult>;

public sealed record MySubscriptionResult(
    Guid PlanId,
    string PlanName,
    string Status,
    DateTimeOffset TrialEndsAtUtc,
    DateTimeOffset? CurrentPeriodEndsAtUtc,
    DateTimeOffset? CanceledAtUtc,
    LatestPaymentSummary? LatestPayment);

public sealed record LatestPaymentSummary(string Status, decimal Amount, DateOnly DueDate, string? InvoiceUrl);
