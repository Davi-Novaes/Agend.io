using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Customers.Application.GetCustomerAuditLog;

public sealed record GetCustomerAuditLogQuery(Guid CustomerId, int Page = 1, int PageSize = 20) : IQuery<GetCustomerAuditLogResult>;

public sealed record AuditLogEntrySummary(
    Guid Id, string Action, string? Before, string? After, string PerformedBy, DateTimeOffset OccurredAtUtc);

public sealed record GetCustomerAuditLogResult(IReadOnlyList<AuditLogEntrySummary> Items, int TotalCount, int Page, int PageSize);
