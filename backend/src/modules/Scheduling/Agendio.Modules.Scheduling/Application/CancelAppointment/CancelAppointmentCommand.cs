using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.CancelAppointment;

public sealed record CancelAppointmentCommand(Guid AppointmentId, bool ByStaff) : ICommand;
