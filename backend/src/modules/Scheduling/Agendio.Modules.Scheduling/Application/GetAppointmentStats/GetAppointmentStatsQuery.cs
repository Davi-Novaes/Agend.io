using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.GetAppointmentStats;

public sealed record GetAppointmentStatsQuery(DateOnly From, DateOnly To) : IQuery<AppointmentStats>;

public sealed record ServiceRevenuePoint(string ServiceName, decimal Total, int Count);

public sealed record ProfessionalRevenuePoint(Guid ResourceId, string ResourceName, decimal Total);

// ScheduledCount/ConfirmedCount fecham o total junto com Completed/NoShow/Cancelled —
// InProgress soma dentro de ConfirmedCount por ser um estado transiente e raro de
// aparecer num agendamento consultado fora do horario exato do atendimento.
public sealed record AppointmentStats(
    int TotalCount, int CompletedCount, int NoShowCount, int CancelledCount, int RescheduledCount,
    int ScheduledCount, int ConfirmedCount,
    decimal NoShowRate, decimal CancellationRate, decimal RescheduleRate,
    IReadOnlyList<ServiceRevenuePoint> RevenueByService, IReadOnlyList<ProfessionalRevenuePoint> RevenueByProfessional);
