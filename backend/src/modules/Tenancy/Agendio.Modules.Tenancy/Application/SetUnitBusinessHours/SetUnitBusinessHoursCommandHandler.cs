using Agendio.Modules.Tenancy.Domain;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.SetUnitBusinessHours;

public sealed class SetUnitBusinessHoursCommandHandler(TenancyDbContext dbContext) : ICommandHandler<SetUnitBusinessHoursCommand>
{
    public async Task<Result> Handle(SetUnitBusinessHoursCommand request, CancellationToken cancellationToken)
    {
        var unit = await dbContext.Units.SingleOrDefaultAsync(u => u.Id == UnitId.From(request.UnitId), cancellationToken);
        if (unit is null)
        {
            return Result.Failure(Error.NotFound("Unit.NotFound", "Unidade nao encontrada."));
        }

        var entries = request.Entries.Select(e => (e.DayOfWeek, e.StartTime, e.EndTime)).ToList();
        var result = unit.SetBusinessHours(entries);
        if (result.IsFailure)
        {
            return result;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
