using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Tenancy.Contracts;

public sealed record TenantLookupResult(
    TenantId TenantId, string Name, string Slug, string TimeZoneId, bool IsActive, string? PrimaryColorHex, string? LogoUrl);
