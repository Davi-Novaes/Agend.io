using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.GetAppointmentStats;

public sealed class GetAppointmentStatsQueryHandler(SchedulingDbContext dbContext, IResourceLookupService resourceLookupService)
    : IQueryHandler<GetAppointmentStatsQuery, AppointmentStats>
{
    public async Task<Result<AppointmentStats>> Handle(GetAppointmentStatsQuery request, CancellationToken cancellationToken)
    {
        var fromUtc = new DateTimeOffset(request.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtcExclusive = new DateTimeOffset(request.To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        // Filtra por inicio do agendamento dentro do periodo — diferente de
        // ListAppointmentsQuery, que usa overlap de janela (semantica de agenda,
        // nao de relatorio: aqui a pergunta e "o que comecou neste periodo").
        var appointments = await dbContext.Appointments.AsNoTracking()
            .Where(a => a.Slot.StartUtc >= fromUtc && a.Slot.StartUtc < toUtcExclusive)
            .Select(a => new { a.Status, a.ServiceName, a.ResourceId, Amount = a.Price.Amount })
            .ToListAsync(cancellationToken);

        var totalCount = appointments.Count;
        var completedCount = appointments.Count(a => a.Status == AppointmentStatus.Completed);
        var noShowCount = appointments.Count(a => a.Status == AppointmentStatus.NoShow);
        var cancelledCount = appointments.Count(a =>
            a.Status == AppointmentStatus.CancelledByCustomer || a.Status == AppointmentStatus.CancelledByStaff);
        var scheduledCount = appointments.Count(a => a.Status == AppointmentStatus.Scheduled);
        var confirmedCount = appointments.Count(a =>
            a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.InProgress);

        var noShowRate = totalCount == 0 ? 0m : Math.Round(noShowCount * 100m / totalCount, 1);
        var cancellationRate = totalCount == 0 ? 0m : Math.Round(cancelledCount * 100m / totalCount, 1);

        // Diferente de completed/no-show/cancelled (contam o AGENDAMENTO em si,
        // filtrado por Slot.StartUtc no periodo), remarcacao e um EVENTO — o
        // mesmo agendamento pode ser remarcado varias vezes, e o horario atual
        // dele pode nem estar mais dentro do periodo pedido. Por isso conta do
        // change log por OccorredAtUtc (quando a remarcacao aconteceu), nao do
        // Appointment por Slot.StartUtc.
        var rescheduledCount = await dbContext.AppointmentChangeLogEntries.AsNoTracking()
            .CountAsync(e => e.ChangeType == AppointmentChangeType.Rescheduled && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtcExclusive,
                cancellationToken);
        var rescheduleRate = totalCount == 0 ? 0m : Math.Round(rescheduledCount * 100m / totalCount, 1);

        var completed = appointments.Where(a => a.Status == AppointmentStatus.Completed).ToList();

        var revenueByService = completed
            .GroupBy(a => a.ServiceName)
            .Select(group => new ServiceRevenuePoint(group.Key, group.Sum(a => a.Amount), group.Count()))
            .OrderByDescending(point => point.Total)
            .ToList();

        var revenueByResourceId = completed
            .GroupBy(a => a.ResourceId)
            .Select(group => new { ResourceId = group.Key, Total = group.Sum(a => a.Amount) })
            .OrderByDescending(group => group.Total)
            .ToList();

        var revenueByProfessional = new List<ProfessionalRevenuePoint>();
        foreach (var group in revenueByResourceId)
        {
            var resource = await resourceLookupService.FindByIdAsync(group.ResourceId, cancellationToken);
            revenueByProfessional.Add(new ProfessionalRevenuePoint(group.ResourceId, resource?.Name ?? "Profissional removido", group.Total));
        }

        return Result.Success(new AppointmentStats(
            totalCount, completedCount, noShowCount, cancelledCount, rescheduledCount, scheduledCount, confirmedCount,
            noShowRate, cancellationRate, rescheduleRate,
            revenueByService, revenueByProfessional));
    }
}
