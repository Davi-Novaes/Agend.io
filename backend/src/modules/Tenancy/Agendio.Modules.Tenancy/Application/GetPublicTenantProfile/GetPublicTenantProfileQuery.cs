using Agendio.Modules.Tenancy.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.GetPublicTenantProfile;

// Resolvido por slug, sem autenticacao: Tenant nao e ITenantOwned (e o proprio
// tenant), entao a leitura nao passa por IHasExplicitTenant/query filter.
public sealed record GetPublicTenantProfileQuery(string Slug) : IQuery<PublicTenantProfile>;

public sealed record PublicBusinessHoursEntry(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public sealed record PublicTenantProfile(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    string? PrimaryColorHex,
    string? LogoUrl,
    string? BannerUrl,
    string? Description,
    string? Phone,
    string? WhatsApp,
    string? Email,
    string? Address,
    string? InstagramUrl,
    string? FacebookUrl,
    string? SecondaryColorHex,
    PublicPageFont Font,
    PublicPageButtonStyle ButtonStyle,
    bool ShowAboutSection,
    bool ShowServicesSection,
    bool ShowTeamSection,
    bool ShowHoursSection,
    bool ShowContactSection,
    IReadOnlyList<PublicBusinessHoursEntry> BusinessHours,
    bool PaymentRequired,
    int DepositPercentage);
