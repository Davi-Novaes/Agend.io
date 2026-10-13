using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.RemoveTenantBanner;

// Mesmo racional de RemoveTenantLogoCommandHandler: o arquivo fica no disco e e
// sobrescrito no proximo upload.
public sealed class RemoveTenantBannerCommandHandler(TenancyDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<RemoveTenantBannerCommand>
{
    public async Task<Result> Handle(RemoveTenantBannerCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        tenant.RemoveBanner();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
