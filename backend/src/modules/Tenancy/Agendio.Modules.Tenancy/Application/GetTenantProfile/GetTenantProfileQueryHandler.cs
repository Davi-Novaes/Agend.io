using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.GetTenantProfile;

public sealed class GetTenantProfileQueryHandler(TenancyDbContext dbContext, ITenantContext tenantContext)
    : IQueryHandler<GetTenantProfileQuery, TenantProfile>
{
    public async Task<Result<TenantProfile>> Handle(GetTenantProfileQuery request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<TenantProfile>(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        var businessHours = tenant.BusinessHours
            .Select(h => new BusinessHoursEntryResult(h.DayOfWeek, h.StartTime, h.EndTime))
            .ToList();

        var profile = new TenantProfile(
            tenant.Name,
            tenant.Slug.Value,
            tenant.PrimaryColorHex,
            tenant.LogoUrl,
            tenant.BannerUrl,
            tenant.Description,
            tenant.Phone?.Value,
            tenant.WhatsApp?.Value,
            tenant.Email?.Value,
            tenant.Address,
            tenant.InstagramUrl,
            tenant.FacebookUrl,
            businessHours);

        return Result.Success(profile);
    }
}
