using Agendio.Modules.Resources.Domain;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.CreateTimeOff;

public sealed class CreateTimeOffCommandHandler(ResourcesDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<CreateTimeOffCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateTimeOffCommand request, CancellationToken cancellationToken)
    {
        var resourceExists = await dbContext.Resources
            .AsNoTracking()
            .AnyAsync(r => r.Id == ResourceId.From(request.ResourceId), cancellationToken);

        if (!resourceExists)
        {
            return Result.Failure<Guid>(Error.NotFound("Resource.NotFound", "Recurso nao encontrado."));
        }

        var timeOffResult = TimeOff.Create(
            tenantContext.TenantId, ResourceId.From(request.ResourceId), request.StartDate, request.EndDate, request.Reason);

        if (timeOffResult.IsFailure)
        {
            return Result.Failure<Guid>(timeOffResult.Error);
        }

        dbContext.TimeOffs.Add(timeOffResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(timeOffResult.Value.Id.Value);
    }
}
