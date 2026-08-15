using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.GetCustomerAppointmentHistory;

public sealed class GetCustomerAppointmentHistoryQueryHandler(
    SchedulingDbContext dbContext, IResourceLookupService resourceLookupService, IClock clock)
    : IQueryHandler<GetCustomerAppointmentHistoryQuery, CustomerAppointmentHistory>
{
    public async Task<Result<CustomerAppointmentHistory>> Handle(GetCustomerAppointmentHistoryQuery request, CancellationToken cancellationToken)
    {
        var appointments = await dbContext.Appointments.AsNoTracking()
            .Where(a => a.CustomerId == request.CustomerId)
            .OrderByDescending(a => a.Slot.StartUtc)
            .Select(a => new
            {
                a.Id,
                a.ServiceName,
                a.ResourceId,
                a.Slot.StartUtc,
                a.Slot.EndUtc,
                a.Status,
                Amount = a.Price.Amount,
                a.Price.Currency,
                a.Notes,
            })
            .ToListAsync(cancellationToken);

        var resourceNames = new Dictionary<Guid, string>();
        foreach (var resourceId in appointments.Select(a => a.ResourceId).Distinct())
        {
            var resource = await resourceLookupService.FindByIdAsync(resourceId, cancellationToken);
            resourceNames[resourceId] = resource?.Name ?? "Profissional removido";
        }

        var items = appointments
            .Select(a => new CustomerAppointmentHistoryItem(
                a.Id.Value, a.ServiceName, a.ResourceId, resourceNames[a.ResourceId],
                a.StartUtc, a.EndUtc, a.Status.ToString(), a.Amount, a.Currency, a.Notes))
            .ToList();

        var completed = appointments.Where(a => a.Status == AppointmentStatus.Completed).ToList();

        var totalVisits = completed.Count;
        var totalSpent = completed.Sum(a => a.Amount);
        var totalSpentCurrency = completed.Count > 0 ? completed[0].Currency : null;
        var lastVisitAtUtc = completed.Count > 0 ? completed.Max(a => a.StartUtc) : (DateTimeOffset?)null;

        var nowUtc = clock.UtcNow;
        var nextAppointmentAtUtc = appointments
            .Where(a => a.Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed && a.StartUtc > nowUtc)
            .Select(a => (DateTimeOffset?)a.StartUtc)
            .OrderBy(startUtc => startUtc)
            .FirstOrDefault();

        var favoriteServiceName = completed
            .GroupBy(a => a.ServiceName)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .FirstOrDefault();

        var favoriteProfessionalName = completed
            .GroupBy(a => a.ResourceId)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => resourceNames[group.Key])
            .Select(group => resourceNames[group.Key])
            .FirstOrDefault();

        var noShowCount = appointments.Count(a => a.Status == AppointmentStatus.NoShow);

        return Result.Success(new CustomerAppointmentHistory(
            items, totalVisits, totalSpent, totalSpentCurrency, lastVisitAtUtc, nextAppointmentAtUtc,
            favoriteServiceName, favoriteProfessionalName, noShowCount));
    }
}
