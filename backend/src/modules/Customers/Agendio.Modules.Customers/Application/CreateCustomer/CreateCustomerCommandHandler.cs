using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Agendio.Modules.Customers.Application.CreateCustomer;

public sealed class CreateCustomerCommandHandler(CustomersDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<CreateCustomerCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customerResult = Domain.Customer.Create(
            tenantContext.TenantId, request.FullName, request.Email, request.Phone,
            request.Notes, request.DateOfBirth, request.CustomData, request.Cpf, request.HealthNotes);

        if (customerResult.IsFailure)
        {
            return Result.Failure<Guid>(customerResult.Error);
        }

        dbContext.Customers.Add(customerResult.Value);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateEmail(ex))
        {
            // Constraint de unicidade (tenant_id, email) do banco e a defesa
            // de verdade contra duplo-clique/retry criando 2 clientes
            // identicos — este catch so traduz pra um erro legivel (BL-26,
            // docs/BACKLOG.md), mesmo padrao de ScheduleAppointmentCommandHandler.
            return Result.Failure<Guid>(
                Error.Conflict("Customer.EmailTaken", "Ja existe um cliente com este e-mail."));
        }

        return Result.Success(customerResult.Value.Id.Value);
    }

    private static bool IsDuplicateEmail(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
