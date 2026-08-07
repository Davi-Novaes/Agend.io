using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.ScheduleAppointment;

public sealed record ScheduleAppointmentCommand(
    Guid CustomerId, Guid ResourceId, Guid ServiceId, DateTimeOffset StartAtUtc, string? Notes) : ICommand<Guid>;
