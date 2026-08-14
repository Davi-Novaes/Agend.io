using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Notifications;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Application.SendCustomerMessage;

public sealed class SendCustomerMessageCommandHandler(
    CustomersDbContext dbContext, ITenantContext tenantContext, IBackgroundJobClient jobClient)
    : ICommandHandler<SendCustomerMessageCommand>
{
    public async Task<Result> Handle(SendCustomerMessageCommand request, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .SingleOrDefaultAsync(c => c.Id == CustomerId.From(request.CustomerId), cancellationToken);

        if (customer is null)
        {
            return Result.Failure(Error.NotFound("Customer.NotFound", "Cliente nao encontrado."));
        }

        if (customer.Email is null)
        {
            return Result.Failure(Error.Validation("Customer.NoEmail", "Este cliente nao tem e-mail cadastrado."));
        }

        jobClient.Enqueue<CustomerMessageEmailJob>(job => job.SendAsync(
            tenantContext.TenantId.Value, customer.Email.Value, customer.FullName, request.Subject, request.Body, CancellationToken.None));

        return Result.Success();
    }
}
