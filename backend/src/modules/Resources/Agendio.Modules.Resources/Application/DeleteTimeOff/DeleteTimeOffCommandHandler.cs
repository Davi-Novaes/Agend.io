using Agendio.Modules.Resources.Domain;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.DeleteTimeOff;

public sealed class DeleteTimeOffCommandHandler(ResourcesDbContext dbContext) : ICommandHandler<DeleteTimeOffCommand>
{
    public async Task<Result> Handle(DeleteTimeOffCommand request, CancellationToken cancellationToken)
    {
        var timeOff = await dbContext.TimeOffs
            .SingleOrDefaultAsync(t => t.Id == TimeOffId.From(request.TimeOffId), cancellationToken);

        if (timeOff is null)
        {
            return Result.Failure(Error.NotFound("TimeOff.NotFound", "Folga nao encontrada."));
        }

        dbContext.TimeOffs.Remove(timeOff);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
