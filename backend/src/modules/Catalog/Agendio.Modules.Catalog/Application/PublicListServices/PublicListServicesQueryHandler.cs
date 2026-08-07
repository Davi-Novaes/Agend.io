using Agendio.Modules.Catalog.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Catalog.Application.PublicListServices;

public sealed class PublicListServicesQueryHandler(CatalogDbContext dbContext)
    : IQueryHandler<PublicListServicesQuery, IReadOnlyList<PublicServiceSummary>>
{
    public async Task<Result<IReadOnlyList<PublicServiceSummary>>> Handle(PublicListServicesQuery request, CancellationToken cancellationToken)
    {
        var services = await dbContext.Services.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        IReadOnlyList<PublicServiceSummary> items = services
            .Select(s => new PublicServiceSummary(s.Id.Value, s.Name, s.Description, s.DurationMinutes, s.Price.Amount, s.Price.Currency, s.Category))
            .ToList();

        return Result.Success(items);
    }
}
