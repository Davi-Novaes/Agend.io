using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.RescheduleAppointment;

// NewDurationMinutes/NewResourceId sao opcionais e retrocompativeis: nulos
// preservam o comportamento de sempre (so muda o horario). Presentes,
// cobrem redimensionar (arrastar a borda do card) e reatribuir profissional
// (arrastar entre colunas) na Agenda, respectivamente — mesmo comando, nao
// um novo, porque ambos sao variacoes de "o slot do agendamento mudou".
public sealed record RescheduleAppointmentCommand(
    Guid AppointmentId,
    DateTimeOffset NewStartAtUtc,
    string? Reason,
    int? NewDurationMinutes = null,
    Guid? NewResourceId = null) : ICommand;
