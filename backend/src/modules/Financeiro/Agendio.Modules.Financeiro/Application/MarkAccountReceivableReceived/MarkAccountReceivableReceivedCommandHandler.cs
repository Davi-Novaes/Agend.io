using Agendio.Modules.Financeiro.Domain;
using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Financeiro.Application.MarkAccountReceivableReceived;

public sealed class MarkAccountReceivableReceivedCommandHandler(FinanceiroDbContext dbContext, IClock clock)
    : ICommandHandler<MarkAccountReceivableReceivedCommand>
{
    public async Task<Result> Handle(MarkAccountReceivableReceivedCommand request, CancellationToken cancellationToken)
    {
        var receivable = await dbContext.AccountsReceivable
            .SingleOrDefaultAsync(a => a.Id == AccountReceivableId.From(request.AccountReceivableId), cancellationToken);

        if (receivable is null)
        {
            return Result.Failure(Error.NotFound("AccountReceivable.NotFound", "Conta a receber nao encontrada."));
        }

        var result = receivable.MarkReceived(clock.UtcNow);
        if (result.IsFailure)
        {
            return result;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
