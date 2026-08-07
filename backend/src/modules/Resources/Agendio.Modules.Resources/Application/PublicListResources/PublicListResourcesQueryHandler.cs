using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.PublicListResources;

public sealed class PublicListResourcesQueryHandler(ResourcesDbContext dbContext)
    : IQueryHandler<PublicListResourcesQuery, IReadOnlyList<PublicResourceSummary>>
{
    public async Task<Result<IReadOnlyList<PublicResourceSummary>>> Handle(PublicListResourcesQuery request, CancellationToken cancellationToken)
    {
        var resources = await dbContext.Resources.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        IReadOnlyList<PublicResourceSummary> items = resources
            .Select(r => new PublicResourceSummary(r.Id.Value, r.Name, r.Type.ToString(), r.Description))
            .ToList();

        return Result.Success(items);
    }
}
