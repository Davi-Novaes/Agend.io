using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Catalog.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Catalog.Application.ListServices;

public sealed class ListServicesQueryHandler(CatalogDbContext dbContext) : IQueryHandler<ListServicesQuery, ListServicesResult>
{
    public async Task<Result<ListServicesResult>> Handle(ListServicesQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Services.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(s => EF.Functions.ILike(s.Name, $"%{search}%"));
        }

        var paged = await query
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .ToPagedItemsAsync(request.Page, request.PageSize, cancellationToken);

        var items = paged.Items
            .Select(s => new ServiceSummary(
                s.Id.Value, s.Name, s.DurationMinutes, s.Price.Amount, s.Price.Currency, s.Category, s.DisplayOrder, s.ImageUrl, s.IsActive))
            .ToList();

        return Result.Success(new ListServicesResult(items, paged.TotalCount, paged.Page, paged.PageSize));
    }
}
