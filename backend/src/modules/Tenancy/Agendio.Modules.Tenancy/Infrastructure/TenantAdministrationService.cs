using Agendio.Modules.Tenancy.Contracts;
using Agendio.Modules.Tenancy.Domain;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Infrastructure;

internal sealed class TenantAdministrationService(TenancyDbContext dbContext) : ITenantAdministrationService
{
    public async Task<IReadOnlyList<TenantLookupResult>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await dbContext.Tenants.AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return tenants.Select(Map).ToList();
    }

    public async Task<Result> SetActiveStatusAsync(TenantId tenantId, bool isActive, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        if (isActive)
        {
            tenant.Activate();
        }
        else
        {
            tenant.Deactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static TenantLookupResult Map(Tenant tenant) =>
        new(tenant.Id, tenant.Name, tenant.Slug.Value, tenant.TimeZoneId, tenant.IsActive, tenant.PrimaryColorHex, tenant.LogoUrl,
            tenant.CreatedAtUtc);
}
