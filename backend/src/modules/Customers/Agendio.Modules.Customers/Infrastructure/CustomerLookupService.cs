using Agendio.Modules.Customers.Application.ListCustomers;
using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.Modules.Scheduling.Contracts;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Infrastructure;

internal sealed class CustomerLookupService(CustomersDbContext dbContext, ICustomerVisitStatsLookupService visitStatsLookup, IClock clock)
    : ICustomerLookupService
{
    public async Task<CustomerLookupResult?> FindByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == CustomerId.From(customerId), cancellationToken);

        return customer is null
            ? null
            : new CustomerLookupResult(customer.Id.Value, customer.FullName, customer.Email?.Value, customer.Phone?.Value, customer.IsActive);
    }

    public async Task<IReadOnlyList<CustomerLookupResult>> FindByIdsAsync(
        IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken = default)
    {
        if (customerIds.Count == 0)
        {
            return [];
        }

        var ids = customerIds.Select(CustomerId.From).ToList();
        var customers = await dbContext.Customers.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(cancellationToken);

        return customers
            .Select(c => new CustomerLookupResult(c.Id.Value, c.FullName, c.Email?.Value, c.Phone?.Value, c.IsActive))
            .ToList();
    }

    public async Task<IReadOnlyList<CustomerLookupResult>> ListActiveWithEmailAsync(CancellationToken cancellationToken = default)
    {
        var customers = await dbContext.Customers.AsNoTracking()
            .Where(c => c.IsActive && c.Email != null)
            .OrderBy(c => c.FullName)
            .ToListAsync(cancellationToken);

        return customers
            .Select(c => new CustomerLookupResult(c.Id.Value, c.FullName, c.Email!.Value, c.Phone?.Value, c.IsActive))
            .ToList();
    }

    public async Task<IReadOnlyList<CustomerLookupResult>> ListActiveBySegmentAsync(
        CustomerSegment? segment, CancellationToken cancellationToken = default)
    {
        var customers = await dbContext.Customers.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.FullName)
            .ToListAsync(cancellationToken);

        if (segment is null)
        {
            return customers
                .Select(c => new CustomerLookupResult(c.Id.Value, c.FullName, c.Email?.Value, c.Phone?.Value, c.IsActive))
                .ToList();
        }

        // Mesmo calculo usado por ListCustomersQueryHandler (Fase 9) — segmento
        // nunca persistido, sempre recalculado na leitura.
        var allStats = await visitStatsLookup.ListAllAsync(cancellationToken);
        var statsByCustomerId = allStats.ToDictionary(s => s.CustomerId);
        var vipSpendThreshold = CustomerSegmentCalculator.CalculateVipSpendThreshold(allStats.Where(s => s.TotalVisits > 0).ToList());
        var nowUtc = clock.UtcNow;

        return customers
            .Where(c => CustomerSegmentCalculator.Calculate(statsByCustomerId.GetValueOrDefault(c.Id.Value), nowUtc, vipSpendThreshold) == segment)
            .Select(c => new CustomerLookupResult(c.Id.Value, c.FullName, c.Email?.Value, c.Phone?.Value, c.IsActive))
            .ToList();
    }
}
