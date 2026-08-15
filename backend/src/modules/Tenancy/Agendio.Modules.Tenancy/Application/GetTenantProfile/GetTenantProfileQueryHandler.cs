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
        var closedDates = tenant.ClosedDates
            .Select(d => new ClosedDateResult(d.Date, d.Reason))
            .OrderBy(d => d.Date)
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
            tenant.SecondaryColorHex,
            tenant.Font,
            tenant.ButtonStyle,
            tenant.ShowAboutSection,
            tenant.ShowServicesSection,
            tenant.ShowTeamSection,
            tenant.ShowHoursSection,
            tenant.ShowContactSection,
            businessHours,
            closedDates,
            tenant.AppointmentBufferMinutes,
            tenant.WhatsAppIntegrationEnabled,
            tenant.WhatsAppPhoneNumberId,
            tenant.WhatsAppAccessToken is not null,
            tenant.WhatsAppScheduledTemplate,
            tenant.WhatsAppReminderTemplate,
            tenant.WhatsAppCancelledTemplate,
            tenant.WhatsAppRescheduledTemplate,
            tenant.WhatsAppConfirmedTemplate,
            tenant.WhatsAppCompletedTemplate,
            tenant.Reminder24hEnabled,
            tenant.Reminder2hEnabled,
            tenant.PostServiceThankYouEnabled,
            tenant.LoyaltyProgramEnabled,
            tenant.LoyaltyVisitsForReward,
            tenant.LoyaltyRewardDescription);

        return Result.Success(profile);
    }
}
