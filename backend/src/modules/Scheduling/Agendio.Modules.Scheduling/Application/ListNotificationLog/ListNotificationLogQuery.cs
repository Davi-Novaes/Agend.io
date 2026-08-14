using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.ListNotificationLog;

public sealed record ListNotificationLogQuery(int Page = 1, int PageSize = 20) : IQuery<ListNotificationLogResult>;

public sealed record NotificationLogItem(
    Guid Id,
    Guid AppointmentId,
    string ServiceName,
    Guid CustomerId,
    string CustomerName,
    string Channel,
    string Trigger,
    string Status,
    DateTimeOffset SentAtUtc,
    string? ErrorMessage);

public sealed record ListNotificationLogResult(IReadOnlyList<NotificationLogItem> Items, int TotalCount, int Page, int PageSize);
