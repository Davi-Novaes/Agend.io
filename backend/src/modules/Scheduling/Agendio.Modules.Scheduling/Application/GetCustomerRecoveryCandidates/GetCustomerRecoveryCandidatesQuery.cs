using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.GetCustomerRecoveryCandidates;

public sealed record GetCustomerRecoveryCandidatesQuery : IQuery<IReadOnlyList<CustomerRecoveryCandidate>>;

public sealed record CustomerRecoveryCandidate(
    Guid CustomerId,
    string CustomerName,
    string? CustomerEmail,
    int AverageIntervalDays,
    int DaysSinceLastVisit,
    int DaysOverdue,
    DateTimeOffset LastVisitAtUtc);
