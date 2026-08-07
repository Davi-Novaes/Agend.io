using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.RescheduleAppointment;

public sealed record RescheduleAppointmentCommand(Guid AppointmentId, DateTimeOffset NewStartAtUtc) : ICommand;
