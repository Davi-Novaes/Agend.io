using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Application.GetAppointmentStats;

public sealed record GetAppointmentStatsQuery(DateOnly From, DateOnly To) : IQuery<AppointmentStats>;

public sealed record ServiceRevenuePoint(string ServiceName, decimal Total);

public sealed record ProfessionalRevenuePoint(Guid ResourceId, string ResourceName, decimal Total);

public sealed record AppointmentStats(
    int TotalCount, int CompletedCount, int NoShowCount, int CancelledCount, int RescheduledCount,
    decimal NoShowRate, decimal CancellationRate, decimal RescheduleRate,
    IReadOnlyList<ServiceRevenuePoint> RevenueByService, IReadOnlyList<ProfessionalRevenuePoint> RevenueByProfessional);
