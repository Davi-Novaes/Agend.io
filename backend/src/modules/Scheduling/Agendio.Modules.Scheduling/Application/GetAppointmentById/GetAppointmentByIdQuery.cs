using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.GetAppointmentById;

public sealed record GetAppointmentByIdQuery(Guid AppointmentId) : IQuery<AppointmentDetails>;

public sealed record AppointmentDetails(
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
