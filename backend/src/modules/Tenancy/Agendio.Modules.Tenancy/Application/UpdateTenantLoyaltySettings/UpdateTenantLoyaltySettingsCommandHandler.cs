using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantLoyaltySettings;

public sealed class UpdateTenantLoyaltySettingsCommandHandler(TenancyDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<UpdateTenantLoyaltySettingsCommand>
{
    public async Task<Result> Handle(UpdateTenantLoyaltySettingsCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        var updateResult = tenant.UpdateLoyaltySettings(request.LoyaltyProgramEnabled, request.LoyaltyVisitsForReward, request.LoyaltyRewardDescription);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
