namespace Agendio.Modules.Scheduling.Domain;

/// <summary>Qual evento do agendamento gerou a mensagem — usado tanto para escolher o template (Fase 6) quanto para o historico de mensagens (Fase 7).</summary>
public enum NotificationTrigger
{
    Scheduled,
    Reminder,
    Cancelled,
    Rescheduled,
    Confirmed,
    Completed,
}
