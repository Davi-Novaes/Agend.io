using Agendio.Modules.Tenancy.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.GetTenantProfile;

public sealed record GetTenantProfileQuery : IQuery<TenantProfile>;

public sealed record BusinessHoursEntryResult(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public sealed record ClosedDateResult(DateOnly Date, string? Reason);

public sealed record TenantProfile(
    string Name,
    string Slug,
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
    IReadOnlyList<BusinessHoursEntryResult> BusinessHours,
    IReadOnlyList<ClosedDateResult> ClosedDates,
    int AppointmentBufferMinutes);
