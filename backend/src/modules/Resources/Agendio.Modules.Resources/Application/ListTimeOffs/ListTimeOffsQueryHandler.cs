using Agendio.Modules.Resources.Domain;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.ListTimeOffs;

public sealed class ListTimeOffsQueryHandler(ResourcesDbContext dbContext) : IQueryHandler<ListTimeOffsQuery, IReadOnlyList<TimeOffSummary>>
{
    public async Task<Result<IReadOnlyList<TimeOffSummary>>> Handle(ListTimeOffsQuery request, CancellationToken cancellationToken)
    {
        var items = await dbContext.TimeOffs.AsNoTracking()
            .Where(t => t.ResourceId == ResourceId.From(request.ResourceId))
            .OrderBy(t => t.StartDate)
            .Select(t => new TimeOffSummary(t.Id.Value, t.StartDate, t.EndDate, t.Reason))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<TimeOffSummary>>(items);
    }
}
