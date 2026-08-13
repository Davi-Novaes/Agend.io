using Agendio.Modules.Resources.Domain;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.SetResourceSpecialties;

public sealed class SetResourceSpecialtiesCommandHandler(ResourcesDbContext dbContext) : ICommandHandler<SetResourceSpecialtiesCommand>
{
    public async Task<Result> Handle(SetResourceSpecialtiesCommand request, CancellationToken cancellationToken)
    {
        var resource = await dbContext.Resources
            .SingleOrDefaultAsync(r => r.Id == ResourceId.From(request.ResourceId), cancellationToken);

        if (resource is null)
        {
            return Result.Failure(Error.NotFound("Resource.NotFound", "Recurso nao encontrado."));
        }

        resource.SetSpecialties(request.Specialties);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
