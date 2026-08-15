using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.CancelWaitlistEntry;

public sealed class CancelWaitlistEntryCommandHandler(SchedulingDbContext dbContext) : ICommandHandler<CancelWaitlistEntryCommand>
{
    public async Task<Result> Handle(CancelWaitlistEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await dbContext.WaitlistEntries
            .SingleOrDefaultAsync(w => w.Id == WaitlistEntryId.From(request.WaitlistEntryId), cancellationToken);

        if (entry is null)
        {
            return Result.Failure(Error.NotFound("Waitlist.NotFound", "Entrada da fila de espera nao encontrada."));
        }

        var result = entry.Cancel();
        if (result.IsFailure)
        {
            return result;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
