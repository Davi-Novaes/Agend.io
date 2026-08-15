using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.ConvertWaitlistEntry;

/// <summary>
/// A equipe confirma manualmente UMA entrada da fila (das possivelmente varias
/// notificadas para a mesma vaga), escolhendo o recurso e o horario exatos —
/// mesma responsabilidade de ScheduleAppointmentCommand, so que o cliente ja
/// vem da entrada da fila em vez de ser escolhido na hora.
/// </summary>
public sealed record ConvertWaitlistEntryCommand(Guid WaitlistEntryId, Guid ResourceId, DateTimeOffset StartAtUtc) : ICommand<Guid>;
