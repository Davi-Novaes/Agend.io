using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Scheduling.Contracts;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Infrastructure;

internal sealed class CustomerPortalAppointmentsLookupService(
    SchedulingDbContext dbContext,
    IResourceLookupService resourceLookup) : ICustomerPortalAppointmentsLookupService
{
    public async Task<IReadOnlyList<CustomerPortalAppointmentLookupResult>> ListForCustomerAsync(
        Guid customerId, CancellationToken cancellationToken = default)
    {
        var appointments = await dbContext.Appointments.AsNoTracking()
            .Where(appointment => appointment.CustomerId == customerId)
            .OrderByDescending(appointment => appointment.Slot.StartUtc)
            .Select(appointment => new
            {
                Id = appointment.Id.Value,
                appointment.ResourceId,
                appointment.ServiceId,
                appointment.ServiceName,
                StartAtUtc = appointment.Slot.StartUtc,
                EndAtUtc = appointment.Slot.EndUtc,
                Price = appointment.Price.Amount,
                Currency = appointment.Price.Currency,
                Status = appointment.Status.ToString(),
            })
            .ToListAsync(cancellationToken);

        var resourceNames = new Dictionary<Guid, string>();
        foreach (var resourceId in appointments.Select(appointment => appointment.ResourceId).Distinct())
        {
            var resource = await resourceLookup.FindByIdAsync(resourceId, cancellationToken);
            resourceNames[resourceId] = resource?.Name ?? "Profissional";
        }

        return appointments.Select(appointment => new CustomerPortalAppointmentLookupResult(
            appointment.Id,
            appointment.ServiceId,
            appointment.ServiceName,
            resourceNames[appointment.ResourceId],
            appointment.StartAtUtc,
            appointment.EndAtUtc,
            appointment.Price,
            appointment.Currency,
            appointment.Status)).ToList();
    }
}
