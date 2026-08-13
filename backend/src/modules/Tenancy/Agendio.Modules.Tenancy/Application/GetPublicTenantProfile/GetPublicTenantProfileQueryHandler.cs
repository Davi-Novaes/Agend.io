using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.GetPublicTenantProfile;

public sealed class GetPublicTenantProfileQueryHandler(TenancyDbContext dbContext)
    : IQueryHandler<GetPublicTenantProfileQuery, PublicTenantProfile>
{
    public async Task<Result<PublicTenantProfile>> Handle(GetPublicTenantProfileQuery request, CancellationToken cancellationToken)
    {
        var slugResult = Slug.Create(request.Slug);
        if (slugResult.IsFailure)
        {
            return Result.Failure<PublicTenantProfile>(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        var tenant = await dbContext.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Slug == slugResult.Value, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<PublicTenantProfile>(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        var businessHours = tenant.BusinessHours
            .Select(h => new PublicBusinessHoursEntry(h.DayOfWeek, h.StartTime, h.EndTime))
            .ToList();

        var profile = new PublicTenantProfile(
            tenant.Id.Value,
            tenant.Name,
            tenant.Slug.Value,
            tenant.IsActive,
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
            tenant.SecondaryColorHex,
            tenant.Font,
            tenant.ButtonStyle,
            tenant.ShowAboutSection,
            tenant.ShowServicesSection,
            tenant.ShowTeamSection,
            tenant.ShowHoursSection,
            tenant.ShowContactSection,
            businessHours);

        return Result.Success(profile);
    }
}
