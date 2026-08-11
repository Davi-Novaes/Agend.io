using Agendio.Modules.Tenancy.Domain;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.UpdateUnit;

public sealed class UpdateUnitCommandHandler(TenancyDbContext dbContext) : ICommandHandler<UpdateUnitCommand>
{
    public async Task<Result> Handle(UpdateUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = await dbContext.Units.SingleOrDefaultAsync(u => u.Id == UnitId.From(request.UnitId), cancellationToken);

        if (unit is null)
        {
            return Result.Failure(Error.NotFound("Unit.NotFound", "Unidade nao encontrada."));
        }

        var updateResult = unit.Update(request.Name, request.Address);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
