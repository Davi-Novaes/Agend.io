using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.ListAppointmentChangeLog;

public sealed record ListAppointmentChangeLogQuery(int Page = 1, int PageSize = 20, Guid? AppointmentId = null, Guid? CustomerId = null)
    : IQuery<ListAppointmentChangeLogResult>;

public sealed record AppointmentChangeLogItem(
    Guid Id,
    Guid AppointmentId,
    string ServiceName,
    Guid CustomerId,
    string CustomerName,
    Guid ResourceId,
    string ResourceName,
    string ChangeType,
    string? Reason,
    DateTimeOffset PreviousStartUtc,
    DateTimeOffset? NewStartUtc,
    bool ByStaff,
    DateTimeOffset OccurredAtUtc);

public sealed record ListAppointmentChangeLogResult(IReadOnlyList<AppointmentChangeLogItem> Items, int TotalCount, int Page, int PageSize);
