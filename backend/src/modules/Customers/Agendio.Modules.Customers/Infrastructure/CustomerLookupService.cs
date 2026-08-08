using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Infrastructure;

internal sealed class CustomerLookupService(CustomersDbContext dbContext) : ICustomerLookupService
{
    public async Task<CustomerLookupResult?> FindByIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == CustomerId.From(customerId), cancellationToken);

        return customer is null ? null : new CustomerLookupResult(customer.Id.Value, customer.FullName, customer.Email?.Value, customer.IsActive);
    }

    public async Task<IReadOnlyList<CustomerLookupResult>> ListActiveWithEmailAsync(CancellationToken cancellationToken = default)
    {
        var customers = await dbContext.Customers.AsNoTracking()
            .Where(c => c.IsActive && c.Email != null)
            .OrderBy(c => c.FullName)
            .ToListAsync(cancellationToken);

        return customers
            .Select(c => new CustomerLookupResult(c.Id.Value, c.FullName, c.Email!.Value, c.IsActive))
            .ToList();
    }
}
