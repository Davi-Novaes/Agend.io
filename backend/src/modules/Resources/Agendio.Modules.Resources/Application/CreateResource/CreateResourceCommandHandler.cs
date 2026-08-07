using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Resources.Application.CreateResource;

public sealed class CreateResourceCommandHandler(ResourcesDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<CreateResourceCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateResourceCommand request, CancellationToken cancellationToken)
    {
        var resourceResult = Domain.Resource.Create(
            tenantContext.TenantId, request.Name, request.Type, request.Capacity, request.Description);

        if (resourceResult.IsFailure)
        {
            return Result.Failure<Guid>(resourceResult.Error);
        }

        dbContext.Resources.Add(resourceResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(resourceResult.Value.Id.Value);
    }
}
