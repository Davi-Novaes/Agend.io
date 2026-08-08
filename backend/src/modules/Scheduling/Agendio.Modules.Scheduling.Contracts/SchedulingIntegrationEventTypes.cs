namespace Agendio.Modules.Scheduling.Contracts;

/// <summary>
/// Nome estavel (sem sufixo de assembly/versao) dos integration events que
/// Scheduling publica no RabbitMQ, para outros modulos consumirem sem
/// referenciar Scheduling.Domain diretamente (proibido pela regra de
/// dependencia entre modulos) — mesmo padrao de TenancyIntegrationEventTypes.
/// </summary>
public static class SchedulingIntegrationEventTypes
{
    public const string AppointmentCompleted = "Agendio.Modules.Scheduling.Domain.AppointmentCompletedDomainEvent";
}
