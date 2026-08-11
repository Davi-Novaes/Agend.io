using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.ListResources;

public sealed class ListResourcesQueryHandler(ResourcesDbContext dbContext) : IQueryHandler<ListResourcesQuery, ListResourcesResult>
{
    public async Task<Result<ListResourcesResult>> Handle(ListResourcesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = dbContext.Resources.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(r => EF.Functions.ILike(r.Name, $"%{search}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var resources = await query
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = resources
            .Select(r => new ResourceSummary(r.Id.Value, r.Name, r.Type, r.Capacity, r.Description, r.IsActive, r.UnitId))
            .ToList();

        return Result.Success(new ListResourcesResult(items, totalCount, page, pageSize));
    }
}
