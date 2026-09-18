using Agendio.Modules.Billing.Contracts;
using Agendio.Modules.Resources.Domain;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.CreateResource;

public sealed class CreateResourceCommandHandler(
    ResourcesDbContext dbContext, ITenantContext tenantContext, IUnitLookupService unitLookup, IPlanLimitsLookupService planLimitsLookup)
    : ICommandHandler<CreateResourceCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateResourceCommand request, CancellationToken cancellationToken)
    {
        if (request.UnitId is { } unitId && !await unitLookup.ExistsAsync(unitId, cancellationToken))
        {
            return Result.Failure<Guid>(Error.Validation("Resource.UnitNotFound", "Unidade nao encontrada."));
        }

        // Limite de plano so se aplica a profissionais (Person) -- sala/equipamento nao contam.
        if (request.Type == ResourceType.Person)
        {
            var limits = await planLimitsLookup.GetActivePlanLimitsAsync(tenantContext.TenantId, cancellationToken);
            if (limits?.MaxProfessionals is { } maxProfessionals)
            {
                var currentCount = await dbContext.Resources.CountAsync(r => r.Type == ResourceType.Person && r.IsActive, cancellationToken);
                if (currentCount >= maxProfessionals)
                {
                    return Result.Failure<Guid>(Error.Forbidden(
                        "Resources.ProfessionalLimitReached",
                        $"Seu plano atual permite no maximo {maxProfessionals} profissional(is). Atualize seu plano para adicionar mais."));
                }
            }
        }

        var resourceResult = Domain.Resource.Create(
            tenantContext.TenantId, request.Name, request.Type, request.Capacity, request.Description, request.UnitId);

        if (resourceResult.IsFailure)
        {
            return Result.Failure<Guid>(resourceResult.Error);
        }

        dbContext.Resources.Add(resourceResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(resourceResult.Value.Id.Value);
    }
}
