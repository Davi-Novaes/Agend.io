using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Customers.Application.CreateCustomer;

public sealed class CreateCustomerCommandHandler(CustomersDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<CreateCustomerCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customerResult = Domain.Customer.Create(
            tenantContext.TenantId, request.FullName, request.Email, request.Phone,
            request.Notes, request.DateOfBirth, request.CustomData);

        if (customerResult.IsFailure)
        {
            return Result.Failure<Guid>(customerResult.Error);
        }

        dbContext.Customers.Add(customerResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(customerResult.Value.Id.Value);
    }
}
