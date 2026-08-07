using Agendio.Modules.Catalog.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Catalog.Application.ListServices;

public sealed class ListServicesQueryHandler(CatalogDbContext dbContext) : IQueryHandler<ListServicesQuery, ListServicesResult>
{
    public async Task<Result<ListServicesResult>> Handle(ListServicesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = dbContext.Services.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(s => EF.Functions.ILike(s.Name, $"%{search}%"));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var services = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = services
            .Select(s => new ServiceSummary(s.Id.Value, s.Name, s.DurationMinutes, s.Price.Amount, s.Price.Currency, s.Category, s.IsActive))
            .ToList();

        return Result.Success(new ListServicesResult(items, totalCount, page, pageSize));
    }
}
