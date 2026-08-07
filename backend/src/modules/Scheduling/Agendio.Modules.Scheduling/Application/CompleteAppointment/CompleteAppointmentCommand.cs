using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.CompleteAppointment;

public sealed record CompleteAppointmentCommand(Guid AppointmentId) : ICommand;
