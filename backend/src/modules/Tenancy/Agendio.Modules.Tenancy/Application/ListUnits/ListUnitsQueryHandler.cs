using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.ListUnits;

public sealed class ListUnitsQueryHandler(TenancyDbContext dbContext) : IQueryHandler<ListUnitsQuery, IReadOnlyList<UnitSummary>>
{
    public async Task<Result<IReadOnlyList<UnitSummary>>> Handle(ListUnitsQuery request, CancellationToken cancellationToken)
    {
        var units = await dbContext.Units.AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new UnitSummary(u.Id.Value, u.Name, u.Address, u.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<UnitSummary>>(units);
    }
}
