using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Application.UpdateCustomer;

public sealed class UpdateCustomerCommandHandler(CustomersDbContext dbContext) : ICommandHandler<UpdateCustomerCommand>
{
    public async Task<Result> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        // O Global Query Filter ja restringe isto ao tenant do JWT do chamador.
        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(c => c.Id == CustomerId.From(request.CustomerId), cancellationToken);

        if (customer is null)
        {
            return Result.Failure(Error.NotFound("Customer.NotFound", "Cliente nao encontrado."));
        }

        var updateResult = customer.Update(
            request.FullName, request.Email, request.Phone, request.Notes, request.DateOfBirth, request.CustomData,
            request.Cpf, request.HealthNotes);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
