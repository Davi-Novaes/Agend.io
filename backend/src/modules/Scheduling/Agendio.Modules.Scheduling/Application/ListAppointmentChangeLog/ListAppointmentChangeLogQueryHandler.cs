using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.ListAppointmentChangeLog;

public sealed class ListAppointmentChangeLogQueryHandler(
    SchedulingDbContext dbContext, ICustomerLookupService customerLookup, IResourceLookupService resourceLookup)
    : IQueryHandler<ListAppointmentChangeLogQuery, ListAppointmentChangeLogResult>
{
    public async Task<Result<ListAppointmentChangeLogResult>> Handle(ListAppointmentChangeLogQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.AppointmentChangeLogEntries.AsNoTracking().AsQueryable();

        if (request.AppointmentId is { } appointmentId)
        {
            query = query.Where(e => e.AppointmentId == Domain.AppointmentId.From(appointmentId));
        }

        if (request.CustomerId is { } customerId)
        {
            query = query.Where(e => e.CustomerId == customerId);
        }

        query = query.OrderByDescending(e => e.OccurredAtUtc);

        var paged = await query.ToPagedItemsAsync(request.Page, request.PageSize, cancellationToken);

        var customerNames = new Dictionary<Guid, string>();
        var resourceNames = new Dictionary<Guid, string>();

        foreach (var itemCustomerId in paged.Items.Select(e => e.CustomerId).Distinct())
        {
            var customer = await customerLookup.FindByIdAsync(itemCustomerId, cancellationToken);
            customerNames[itemCustomerId] = customer?.FullName ?? "Cliente removido";
        }

        foreach (var resourceId in paged.Items.Select(e => e.ResourceId).Distinct())
        {
            var resource = await resourceLookup.FindByIdAsync(resourceId, cancellationToken);
            resourceNames[resourceId] = resource?.Name ?? "Profissional removido";
        }

        var items = paged.Items
            .Select(e => new AppointmentChangeLogItem(
                e.Id.Value,
                e.AppointmentId.Value,
                e.ServiceName,
                e.CustomerId,
                customerNames[e.CustomerId],
                e.ResourceId,
                resourceNames[e.ResourceId],
                e.ChangeType.ToString(),
                e.Reason,
                e.PreviousStartUtc,
                e.NewStartUtc,
                e.ByStaff,
                e.OccurredAtUtc))
            .ToList();

        return Result.Success(new ListAppointmentChangeLogResult(items, paged.TotalCount, paged.Page, paged.PageSize));
    }
}
