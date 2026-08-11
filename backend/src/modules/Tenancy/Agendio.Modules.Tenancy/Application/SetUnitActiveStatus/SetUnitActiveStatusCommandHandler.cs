using Agendio.Modules.Tenancy.Domain;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.SetUnitActiveStatus;

public sealed class SetUnitActiveStatusCommandHandler(TenancyDbContext dbContext) : ICommandHandler<SetUnitActiveStatusCommand>
{
    public async Task<Result> Handle(SetUnitActiveStatusCommand request, CancellationToken cancellationToken)
    {
        var unit = await dbContext.Units.SingleOrDefaultAsync(u => u.Id == UnitId.From(request.UnitId), cancellationToken);

        if (unit is null)
        {
            return Result.Failure(Error.NotFound("Unit.NotFound", "Unidade nao encontrada."));
        }

        if (request.IsActive)
        {
            unit.Activate();
        }
        else
        {
            unit.Deactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
