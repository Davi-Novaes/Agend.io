using Agendio.Modules.Scheduling.Application.GetAppointmentStats;
using Agendio.Modules.Scheduling.Contracts;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Scheduling.Infrastructure;

// Delega para o handler ja existente via IDispatcher (chamada intra-modulo,
// permitida) em vez de duplicar a logica de agregacao aqui.
public sealed class AppointmentStatsLookupService(IDispatcher dispatcher) : IAppointmentStatsLookupService
{
    public async Task<AppointmentStatsLookupResult> GetStatsAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.Query(new GetAppointmentStatsQuery(from, to), cancellationToken);
        var stats = result.Value;

        return new AppointmentStatsLookupResult(
            stats.TotalCount, stats.CompletedCount, stats.NoShowCount, stats.CancelledCount, stats.RescheduledCount,
            stats.NoShowRate, stats.CancellationRate, stats.RescheduleRate,
            stats.RevenueByService.Select(p => new ServiceRevenueLookupPoint(p.ServiceName, p.Total)).ToList(),
            stats.RevenueByProfessional.Select(p => new ProfessionalRevenueLookupPoint(p.ResourceId, p.ResourceName, p.Total)).ToList());
    }
}
