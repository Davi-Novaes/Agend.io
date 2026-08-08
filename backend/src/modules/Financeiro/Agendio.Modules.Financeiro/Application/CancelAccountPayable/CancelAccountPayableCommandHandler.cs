using Agendio.Modules.Financeiro.Domain;
using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Financeiro.Application.CancelAccountPayable;

public sealed class CancelAccountPayableCommandHandler(FinanceiroDbContext dbContext) : ICommandHandler<CancelAccountPayableCommand>
{
    public async Task<Result> Handle(CancelAccountPayableCommand request, CancellationToken cancellationToken)
    {
        var payable = await dbContext.AccountsPayable
            .SingleOrDefaultAsync(a => a.Id == AccountPayableId.From(request.AccountPayableId), cancellationToken);

        if (payable is null)
        {
            return Result.Failure(Error.NotFound("AccountPayable.NotFound", "Conta a pagar nao encontrada."));
        }

        var result = payable.Cancel();
        if (result.IsFailure)
        {
            return result;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
