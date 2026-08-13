using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Tenancy.Application.GetTenantProfile;

public sealed record GetTenantProfileQuery : IQuery<TenantProfile>;

public sealed record BusinessHoursEntryResult(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

public sealed record TenantProfile(
    string Name,
    string Slug,
    string? PrimaryColorHex,
    string? LogoUrl,
    string? Description,
    string? Phone,
    string? WhatsApp,
    string? Email,
    string? Address,
    string? InstagramUrl,
    string? FacebookUrl,
    IReadOnlyList<BusinessHoursEntryResult> BusinessHours);
