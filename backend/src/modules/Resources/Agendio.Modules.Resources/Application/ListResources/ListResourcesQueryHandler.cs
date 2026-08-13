using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.ListResources;

public sealed class ListResourcesQueryHandler(ResourcesDbContext dbContext) : IQueryHandler<ListResourcesQuery, ListResourcesResult>
{
    public async Task<Result<ListResourcesResult>> Handle(ListResourcesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Resources.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(r => EF.Functions.ILike(r.Name, $"%{search}%"));
        }

        var paged = await query.OrderBy(r => r.Name).ToPagedItemsAsync(request.Page, request.PageSize, cancellationToken);

        var items = paged.Items
            .Select(r => new ResourceSummary(
                r.Id.Value, r.Name, r.Type, r.Capacity, r.Description, r.IsActive, r.UnitId, r.PhotoUrl, r.Specialties.ToList()))
            .ToList();

        return Result.Success(new ListResourcesResult(items, paged.TotalCount, paged.Page, paged.PageSize));
    }
}
