using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.MarkAppointmentNoShow;

public sealed record MarkAppointmentNoShowCommand(Guid AppointmentId) : ICommand;
