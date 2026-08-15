using Agendio.Modules.Scheduling.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.ListWaitlist;

public sealed record ListWaitlistQuery(
    int Page = 1, int PageSize = 20, WaitlistStatus? Status = null, Guid? ServiceId = null, Guid? ResourceId = null)
    : IQuery<ListWaitlistResult>;

public sealed record WaitlistEntryItem(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    Guid? ResourceId,
    string? ResourceName,
    Guid ServiceId,
    string ServiceName,
    DateOnly PreferredDate,
    string? Notes,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? NotifiedAtUtc);

public sealed record ListWaitlistResult(IReadOnlyList<WaitlistEntryItem> Items, int TotalCount, int Page, int PageSize);
