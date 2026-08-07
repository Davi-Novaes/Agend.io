using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Billing.Application.ListPlans;

public sealed record ListPlansQuery : IQuery<IReadOnlyList<PlanSummary>>;

public sealed record PlanSummary(Guid Id, string Name, decimal PriceAmount, string Currency, string BillingCycle);
