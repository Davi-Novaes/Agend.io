namespace Agendio.Modules.Scheduling.Contracts;

/// <summary>
/// Leitura agregada de estatisticas de agendamento — usada pelo Assistente (Fase
/// 22) para responder perguntas em linguagem natural sem o modulo Assistant
/// precisar ler tabela do Scheduling diretamente.
/// </summary>
public interface IAppointmentStatsLookupService
{
    Task<AppointmentStatsLookupResult> GetStatsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public sealed record ServiceRevenueLookupPoint(string ServiceName, decimal Total);

public sealed record ProfessionalRevenueLookupPoint(Guid ResourceId, string ResourceName, decimal Total);

public sealed record AppointmentStatsLookupResult(
    int TotalCount, int CompletedCount, int NoShowCount, int CancelledCount, int RescheduledCount,
    decimal NoShowRate, decimal CancellationRate, decimal RescheduleRate,
    IReadOnlyList<ServiceRevenueLookupPoint> RevenueByService, IReadOnlyList<ProfessionalRevenueLookupPoint> RevenueByProfessional);
