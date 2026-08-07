using Agendio.Modules.Resources.Domain;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.DeactivateResource;

public sealed class SetResourceActiveStatusCommandHandler(ResourcesDbContext dbContext) : ICommandHandler<SetResourceActiveStatusCommand>
{
    public async Task<Result> Handle(SetResourceActiveStatusCommand request, CancellationToken cancellationToken)
    {
        var resource = await dbContext.Resources
            .SingleOrDefaultAsync(r => r.Id == ResourceId.From(request.ResourceId), cancellationToken);

        if (resource is null)
        {
            return Result.Failure(Error.NotFound("Resource.NotFound", "Recurso nao encontrado."));
        }

        if (request.IsActive)
        {
            resource.Activate();
        }
        else
        {
            resource.Deactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
