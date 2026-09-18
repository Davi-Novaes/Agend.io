using Agendio.Modules.Billing.Contracts;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.CreateUnit;

public sealed class CreateUnitCommandHandler(TenancyDbContext dbContext, ITenantContext tenantContext, IPlanLimitsLookupService planLimitsLookup)
    : ICommandHandler<CreateUnitCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateUnitCommand request, CancellationToken cancellationToken)
    {
        var limits = await planLimitsLookup.GetActivePlanLimitsAsync(tenantContext.TenantId, cancellationToken);
        if (limits?.MaxUnits is { } maxUnits)
        {
            var currentCount = await dbContext.Units.CountAsync(u => u.IsActive, cancellationToken);
            if (currentCount >= maxUnits)
            {
                return Result.Failure<Guid>(Error.Forbidden(
                    "Tenancy.UnitLimitReached",
                    $"Seu plano atual permite no maximo {maxUnits} unidade(s). Atualize seu plano para adicionar mais."));
            }
        }

        var unitResult = Domain.Unit.Create(tenantContext.TenantId, request.Name, request.Address, request.City, request.State, request.Country);

        if (unitResult.IsFailure)
        {
            return Result.Failure<Guid>(unitResult.Error);
        }

        dbContext.Units.Add(unitResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(unitResult.Value.Id.Value);
    }
}
