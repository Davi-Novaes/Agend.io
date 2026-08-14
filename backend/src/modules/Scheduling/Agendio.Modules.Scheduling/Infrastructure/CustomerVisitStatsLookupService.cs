using Agendio.Modules.Scheduling.Contracts;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Infrastructure;

public sealed class CustomerVisitStatsLookupService(SchedulingDbContext dbContext) : ICustomerVisitStatsLookupService
{
    public async Task<IReadOnlyList<CustomerVisitStatsLookupResult>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        // O Global Query Filter ja restringe isto ao tenant corrente.
        var grouped = await dbContext.Appointments.AsNoTracking()
            .GroupBy(a => a.CustomerId)
            .Select(group => new
            {
                CustomerId = group.Key,
                TotalVisits = group.Count(a => a.Status == AppointmentStatus.Completed),
                LastVisitAtUtc = group.Where(a => a.Status == AppointmentStatus.Completed).Select(a => (DateTimeOffset?)a.Slot.StartUtc).Max(),
                NoShowCount = group.Count(a => a.Status == AppointmentStatus.NoShow),
                TotalSpent = group.Where(a => a.Status == AppointmentStatus.Completed).Sum(a => (decimal?)a.Price.Amount) ?? 0m,
            })
            .ToListAsync(cancellationToken);

        return grouped
            .Select(g => new CustomerVisitStatsLookupResult(g.CustomerId, g.TotalVisits, g.LastVisitAtUtc, g.NoShowCount, g.TotalSpent))
            .ToList();
    }
}
