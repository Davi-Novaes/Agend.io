using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.GetCustomerAppointmentHistory;

public sealed record GetCustomerAppointmentHistoryQuery(Guid CustomerId) : IQuery<CustomerAppointmentHistory>;

public sealed record CustomerAppointmentHistoryItem(
    Guid AppointmentId,
    string ServiceName,
    Guid ResourceId,
    string ProfessionalName,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Status,
    decimal Price,
    string Currency,
    string? Notes);

public sealed record CustomerAppointmentHistory(
    IReadOnlyList<CustomerAppointmentHistoryItem> Items,
    int TotalVisits,
    decimal TotalSpent,
    string? TotalSpentCurrency,
    DateTimeOffset? LastVisitAtUtc,
    DateTimeOffset? NextAppointmentAtUtc,
    string? FavoriteServiceName,
    string? FavoriteProfessionalName,
    int NoShowCount);
