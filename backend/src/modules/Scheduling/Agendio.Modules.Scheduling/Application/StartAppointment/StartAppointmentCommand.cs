using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.StartAppointment;

public sealed record StartAppointmentCommand(Guid AppointmentId) : ICommand;
