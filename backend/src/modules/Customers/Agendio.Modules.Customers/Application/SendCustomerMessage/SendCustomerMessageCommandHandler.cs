using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Notifications;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Application.SendCustomerMessage;

public sealed class SendCustomerMessageCommandHandler(
    CustomersDbContext dbContext, ITenantContext tenantContext, IBackgroundJobClient jobClient, IClock clock)
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

        // Pedido explicito do usuario (2026-09-05): quem ja recebeu uma
        // mensagem nao deve continuar aparecendo no card "Clientes para
        // recuperar" (ver GetCustomerRecoveryCandidatesQueryHandler).
        customer.MarkContacted(clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
