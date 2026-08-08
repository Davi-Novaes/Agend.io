using Agendio.Modules.Financeiro.Domain;
using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Financeiro.Application.MarkAccountPayablePaid;

public sealed class MarkAccountPayablePaidCommandHandler(FinanceiroDbContext dbContext, IClock clock) : ICommandHandler<MarkAccountPayablePaidCommand>
{
    public async Task<Result> Handle(MarkAccountPayablePaidCommand request, CancellationToken cancellationToken)
    {
        var payable = await dbContext.AccountsPayable
            .SingleOrDefaultAsync(a => a.Id == AccountPayableId.From(request.AccountPayableId), cancellationToken);

        if (payable is null)
        {
            return Result.Failure(Error.NotFound("AccountPayable.NotFound", "Conta a pagar nao encontrada."));
        }

        var result = payable.MarkPaid(clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
