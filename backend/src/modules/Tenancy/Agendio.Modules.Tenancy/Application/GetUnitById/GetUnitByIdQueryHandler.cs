using Agendio.Modules.Tenancy.Domain;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.GetUnitById;

public sealed class GetUnitByIdQueryHandler(TenancyDbContext dbContext) : IQueryHandler<GetUnitByIdQuery, UnitDetails>
{
    public async Task<Result<UnitDetails>> Handle(GetUnitByIdQuery request, CancellationToken cancellationToken)
    {
        var unit = await dbContext.Units.AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == UnitId.From(request.UnitId), cancellationToken);

        if (unit is null)
        {
            return Result.Failure<UnitDetails>(Error.NotFound("Unit.NotFound", "Unidade nao encontrada."));
        }

        return Result.Success(new UnitDetails(unit.Id.Value, unit.Name, unit.Address, unit.IsActive));
    }
}
