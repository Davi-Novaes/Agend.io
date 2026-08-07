using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Platform.Application.ListTenants;

public sealed class ListTenantsQueryHandler(ITenantAdministrationService tenantAdministrationService)
    : IQueryHandler<ListTenantsQuery, IReadOnlyList<TenantAdminSummary>>
{
    public async Task<Result<IReadOnlyList<TenantAdminSummary>>> Handle(ListTenantsQuery request, CancellationToken cancellationToken)
    {
        var tenants = await tenantAdministrationService.ListAllAsync(cancellationToken);

        IReadOnlyList<TenantAdminSummary> summaries = tenants
            .Select(t => new TenantAdminSummary(t.TenantId.Value, t.Name, t.Slug, t.TimeZoneId, t.IsActive))
            .ToList();

        return Result.Success(summaries);
    }
}
