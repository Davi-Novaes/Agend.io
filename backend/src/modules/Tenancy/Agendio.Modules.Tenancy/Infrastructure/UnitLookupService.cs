using Agendio.Modules.Tenancy.Contracts;
using Agendio.Modules.Tenancy.Domain;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Infrastructure;

internal sealed class UnitLookupService(TenancyDbContext dbContext) : IUnitLookupService
{
    public Task<bool> ExistsAsync(Guid unitId, CancellationToken cancellationToken = default) =>
        dbContext.Units.AsNoTracking().AnyAsync(u => u.Id == UnitId.From(unitId) && u.IsActive, cancellationToken);
}
