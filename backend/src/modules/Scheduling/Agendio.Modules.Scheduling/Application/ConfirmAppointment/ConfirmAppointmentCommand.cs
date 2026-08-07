using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.ConfirmAppointment;

public sealed record ConfirmAppointmentCommand(Guid AppointmentId) : ICommand;
