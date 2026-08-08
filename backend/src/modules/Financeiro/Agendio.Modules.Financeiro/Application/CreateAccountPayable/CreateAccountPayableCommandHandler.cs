using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Financeiro.Application.CreateAccountPayable;

public sealed class CreateAccountPayableCommandHandler(FinanceiroDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<CreateAccountPayableCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateAccountPayableCommand request, CancellationToken cancellationToken)
    {
        var amountResult = Money.Create(request.Amount);
        if (amountResult.IsFailure)
        {
            return Result.Failure<Guid>(amountResult.Error);
        }

        var payableResult = Domain.AccountPayable.Create(
            tenantContext.TenantId, request.Description, amountResult.Value, request.DueDate, request.Category);

        if (payableResult.IsFailure)
        {
            return Result.Failure<Guid>(payableResult.Error);
        }

        dbContext.AccountsPayable.Add(payableResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(payableResult.Value.Id.Value);
    }
}
