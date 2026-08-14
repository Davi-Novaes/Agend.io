using Agendio.Modules.Tenancy.Contracts;
using Agendio.Modules.Tenancy.Domain;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Infrastructure;

internal sealed class TenantLookupService(TenancyDbContext dbContext) : ITenantLookupService
{
    public async Task<TenantLookupResult?> FindByIdAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        return tenant is null ? null : Map(tenant);
    }

    public async Task<TenantLookupResult?> FindBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        // Compara o Value Object inteiro (nao ".Slug.Value") de proposito: ver
        // comentario equivalente em CreateTenantCommandHandler.
        var slugResult = Slug.Create(slug);
        if (slugResult.IsFailure)
        {
            return null;
        }

        var tenant = await dbContext.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Slug == slugResult.Value, cancellationToken);

        return tenant is null ? null : Map(tenant);
    }

    public async Task<TenantAvailabilityInfo?> GetAvailabilityInfoAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return null;
        }

        var businessHours = tenant.BusinessHours
            .Select(h => new BusinessHoursLookup(h.DayOfWeek, h.StartTime, h.EndTime))
            .ToList();
        var closedDates = tenant.ClosedDates.Select(d => d.Date).ToList();

        return new TenantAvailabilityInfo(tenant.TimeZoneId, businessHours, closedDates, tenant.AppointmentBufferMinutes);
    }

    public async Task<TenantWhatsAppSettings?> GetWhatsAppSettingsAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return null;
        }

        return new TenantWhatsAppSettings(
            tenant.WhatsAppIntegrationEnabled,
            tenant.WhatsAppPhoneNumberId,
            tenant.WhatsAppAccessToken,
            tenant.WhatsAppScheduledTemplate,
            tenant.WhatsAppReminderTemplate,
            tenant.WhatsAppCancelledTemplate,
            tenant.WhatsAppRescheduledTemplate,
            tenant.WhatsAppConfirmedTemplate,
            tenant.WhatsAppCompletedTemplate);
    }

    private static TenantLookupResult Map(Tenant tenant) =>
        new(tenant.Id, tenant.Name, tenant.Slug.Value, tenant.TimeZoneId, tenant.IsActive, tenant.PrimaryColorHex, tenant.LogoUrl);
}
