using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Application.DeactivateCustomer;

public sealed class SetCustomerActiveStatusCommandHandler(CustomersDbContext dbContext)
    : ICommandHandler<SetCustomerActiveStatusCommand>
{
    public async Task<Result> Handle(SetCustomerActiveStatusCommand request, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(c => c.Id == CustomerId.From(request.CustomerId), cancellationToken);

        if (customer is null)
        {
            return Result.Failure(Error.NotFound("Customer.NotFound", "Cliente nao encontrado."));
        }

        if (request.IsActive)
        {
            customer.Activate();
        }
        else
        {
            customer.Deactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
