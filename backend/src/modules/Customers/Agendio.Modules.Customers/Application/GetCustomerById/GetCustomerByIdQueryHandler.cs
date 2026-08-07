using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Application.GetCustomerById;

public sealed class GetCustomerByIdQueryHandler(CustomersDbContext dbContext) : IQueryHandler<GetCustomerByIdQuery, CustomerDetails>
{
    public async Task<Result<CustomerDetails>> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == CustomerId.From(request.CustomerId), cancellationToken);

        if (customer is null)
        {
            return Result.Failure<CustomerDetails>(Error.NotFound("Customer.NotFound", "Cliente nao encontrado."));
        }

        var details = new CustomerDetails(
            customer.Id.Value, customer.FullName, customer.Email?.Value, customer.Phone?.Value,
            customer.Notes, customer.DateOfBirth, customer.CustomData, customer.IsActive, customer.CreatedAtUtc);

        return Result.Success(details);
    }
}
