using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantPageCustomization;

public sealed class UpdateTenantPageCustomizationCommandHandler(TenancyDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<UpdateTenantPageCustomizationCommand>
{
    public async Task<Result> Handle(UpdateTenantPageCustomizationCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        var updateResult = tenant.UpdatePageCustomization(
            request.SecondaryColorHex,
            request.Font,
            request.ButtonStyle,
            request.ShowAboutSection,
            request.ShowServicesSection,
            request.ShowTeamSection,
            request.ShowHoursSection,
            request.ShowContactSection);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
