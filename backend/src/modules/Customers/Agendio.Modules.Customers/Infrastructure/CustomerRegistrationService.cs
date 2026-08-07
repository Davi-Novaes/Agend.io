using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Infrastructure;

internal sealed class CustomerRegistrationService(CustomersDbContext dbContext, ITenantContext tenantContext) : ICustomerRegistrationService
{
    public async Task<Guid> FindOrRegisterByEmailAsync(
        string fullName, string email, string? phone, CancellationToken cancellationToken = default)
    {
        var emailResult = Email.Create(email);
        if (emailResult.IsFailure)
        {
            throw new ArgumentException(emailResult.Error.Message, nameof(email));
        }

        // Compara o Value Object inteiro (ja normalizado para minusculo por
        // Email.Create), nunca ".Value" — ver comentario equivalente em
        // TenantLookupService sobre traducao de predicado do EF Core.
        var existing = await dbContext.Customers
            .SingleOrDefaultAsync(c => c.Email == emailResult.Value, cancellationToken);

        if (existing is not null)
        {
            return existing.Id.Value;
        }

        var customerResult = Customer.Create(tenantContext.TenantId, fullName, email, phone, notes: null, dateOfBirth: null);
        if (customerResult.IsFailure)
        {
            throw new ArgumentException(customerResult.Error.Message, nameof(fullName));
        }

        dbContext.Customers.Add(customerResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return customerResult.Value.Id.Value;
    }
}
