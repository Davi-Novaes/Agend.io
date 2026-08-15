using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.ListWaitlist;

public sealed class ListWaitlistQueryHandler(SchedulingDbContext dbContext, ICustomerLookupService customerLookup, IResourceLookupService resourceLookup)
    : IQueryHandler<ListWaitlistQuery, ListWaitlistResult>
{
    public async Task<Result<ListWaitlistResult>> Handle(ListWaitlistQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.WaitlistEntries.AsNoTracking().AsQueryable();

        if (request.Status is { } status)
        {
            query = query.Where(w => w.Status == status);
        }

        if (request.ServiceId is { } serviceId)
        {
            query = query.Where(w => w.ServiceId == serviceId);
        }

        if (request.ResourceId is { } resourceIdFilter)
        {
            query = query.Where(w => w.ResourceId == resourceIdFilter);
        }

        query = query.OrderBy(w => w.CreatedAtUtc);

        var paged = await query.ToPagedItemsAsync(request.Page, request.PageSize, cancellationToken);

        var customerNames = new Dictionary<Guid, (string Name, string? Email, string? Phone)>();
        var resourceNames = new Dictionary<Guid, string>();

        foreach (var customerId in paged.Items.Select(w => w.CustomerId).Distinct())
        {
            var customer = await customerLookup.FindByIdAsync(customerId, cancellationToken);
            customerNames[customerId] = customer is null
                ? ("Cliente removido", null, null)
                : (customer.FullName, customer.Email, customer.Phone);
        }

        foreach (var resourceId in paged.Items.Where(w => w.ResourceId is not null).Select(w => w.ResourceId!.Value).Distinct())
        {
            var resource = await resourceLookup.FindByIdAsync(resourceId, cancellationToken);
            resourceNames[resourceId] = resource?.Name ?? "Profissional removido";
        }

        var items = paged.Items
            .Select(w =>
            {
                var (name, email, phone) = customerNames[w.CustomerId];
                return new WaitlistEntryItem(
                    w.Id.Value,
                    w.CustomerId,
                    name,
                    email,
                    phone,
                    w.ResourceId,
                    w.ResourceId is { } resourceId ? resourceNames[resourceId] : null,
                    w.ServiceId,
                    w.ServiceName,
                    w.PreferredDate,
                    w.Notes,
                    w.Status.ToString(),
                    w.CreatedAtUtc,
                    w.NotifiedAtUtc);
            })
            .ToList();

        return Result.Success(new ListWaitlistResult(items, paged.TotalCount, paged.Page, paged.PageSize));
    }
}
