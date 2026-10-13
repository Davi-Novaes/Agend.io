using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.RemoveTenantLogo;

// O arquivo fica no disco: o caminho e fixo por tenant, entao o proximo upload
// o sobrescreve, e sem LogoUrl nenhuma pagina aponta mais pra ele.
public sealed class RemoveTenantLogoCommandHandler(TenancyDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<RemoveTenantLogoCommand>
{
    public async Task<Result> Handle(RemoveTenantLogoCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        tenant.RemoveLogo();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
