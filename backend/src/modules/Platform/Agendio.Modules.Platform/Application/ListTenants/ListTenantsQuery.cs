using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Platform.Application.ListTenants;

public sealed record ListTenantsQuery : IQuery<IReadOnlyList<TenantAdminSummary>>;

public sealed record TenantAdminSummary(Guid Id, string Name, string Slug, string TimeZoneId, bool IsActive);
