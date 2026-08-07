using Agendio.Modules.Resources.Domain;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.UpdateResource;

public sealed class UpdateResourceCommandHandler(ResourcesDbContext dbContext) : ICommandHandler<UpdateResourceCommand>
{
    public async Task<Result> Handle(UpdateResourceCommand request, CancellationToken cancellationToken)
    {
        var resource = await dbContext.Resources
            .SingleOrDefaultAsync(r => r.Id == ResourceId.From(request.ResourceId), cancellationToken);

        if (resource is null)
        {
            return Result.Failure(Error.NotFound("Resource.NotFound", "Recurso nao encontrado."));
        }

        var updateResult = resource.Update(request.Name, request.Type, request.Capacity, request.Description);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
