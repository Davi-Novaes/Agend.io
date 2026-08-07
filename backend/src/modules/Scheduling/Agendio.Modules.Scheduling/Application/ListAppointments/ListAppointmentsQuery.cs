using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.ListAppointments;

public sealed record ListAppointmentsQuery(DateTimeOffset FromUtc, DateTimeOffset ToUtc, Guid? ResourceId) : IQuery<IReadOnlyList<AppointmentSummary>>;

public sealed record AppointmentSummary(
    Guid Id,
    Guid CustomerId,
    Guid ResourceId,
    Guid ServiceId,
    string ServiceName,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string Status,
    decimal Price,
    string Currency,
    string? Notes);
