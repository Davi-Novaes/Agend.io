using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Tenancy.Application.CreateUnit;

public sealed class CreateUnitCommandHandler(TenancyDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<CreateUnitCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateUnitCommand request, CancellationToken cancellationToken)
    {
        var unitResult = Domain.Unit.Create(tenantContext.TenantId, request.Name, request.Address);

        if (unitResult.IsFailure)
        {
            return Result.Failure<Guid>(unitResult.Error);
        }

        dbContext.Units.Add(unitResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(unitResult.Value.Id.Value);
    }
}
