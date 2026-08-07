using Agendio.Modules.Resources.Domain;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.SetWorkingHours;

public sealed class SetResourceWorkingHoursCommandHandler(ResourcesDbContext dbContext) : ICommandHandler<SetResourceWorkingHoursCommand>
{
    public async Task<Result> Handle(SetResourceWorkingHoursCommand request, CancellationToken cancellationToken)
    {
        var resource = await dbContext.Resources
            .SingleOrDefaultAsync(r => r.Id == ResourceId.From(request.ResourceId), cancellationToken);

        if (resource is null)
        {
            return Result.Failure(Error.NotFound("Resource.NotFound", "Recurso nao encontrado."));
        }

        var entries = request.Entries.Select(e => (e.DayOfWeek, e.StartTime, e.EndTime)).ToList();

        var setResult = resource.SetWorkingHours(entries);
        if (setResult.IsFailure)
        {
            return setResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
