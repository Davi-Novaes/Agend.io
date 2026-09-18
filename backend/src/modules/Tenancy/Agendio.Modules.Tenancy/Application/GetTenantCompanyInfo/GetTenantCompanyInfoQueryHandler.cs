using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.GetTenantCompanyInfo;

public sealed class GetTenantCompanyInfoQueryHandler(TenancyDbContext dbContext, ITenantContext tenantContext)
    : IQueryHandler<GetTenantCompanyInfoQuery, TenantCompanyInfo>
{
    public async Task<Result<TenantCompanyInfo>> Handle(GetTenantCompanyInfoQuery request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<TenantCompanyInfo>(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        return Result.Success(new TenantCompanyInfo(
            tenant.Name, tenant.LegalName, tenant.Document?.Value, tenant.City, tenant.State, tenant.ZipCode));
    }
}
