using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Customers.Application.CustomerPortal;

/// <summary>
/// Responde sempre sucesso para nao revelar se o e-mail pertence a um cliente.
/// O codigo e gerado dentro do job, nunca aparece nos argumentos persistidos do Hangfire.
/// </summary>
public sealed class RequestCustomerPortalCodeCommandHandler(
    CustomersDbContext dbContext,
    CustomerPortalAccessCodeSender accessCodeSender) : ICommandHandler<RequestCustomerPortalCodeCommand>
{
    public async Task<Result> Handle(RequestCustomerPortalCodeCommand request, CancellationToken cancellationToken)
    {
        var emailResult = Email.Create(request.Email);
        if (emailResult.IsFailure)
        {
            return Result.Success();
        }

        var customer = await dbContext.Customers.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Email == emailResult.Value, cancellationToken);

        if (customer is null || !customer.IsActive)
        {
            return Result.Success();
        }

        await accessCodeSender.SendIfQuotaAvailableAsync(
            request.TenantId, customer.Id.Value, emailResult.Value.Value, cancellationToken);

        return Result.Success();
    }
}
