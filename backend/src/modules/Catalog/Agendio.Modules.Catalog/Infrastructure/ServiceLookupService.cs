using Agendio.Modules.Catalog.Contracts;
using Agendio.Modules.Catalog.Domain;
using Agendio.Modules.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Catalog.Infrastructure;

internal sealed class ServiceLookupService(CatalogDbContext dbContext) : IServiceLookupService
{
    public async Task<ServiceLookupResult?> FindByIdAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        var service = await dbContext.Services.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == ServiceId.From(serviceId), cancellationToken);

        return service is null
            ? null
            : new ServiceLookupResult(service.Id.Value, service.Name, service.DurationMinutes, service.Price.Amount, service.Price.Currency, service.IsActive);
    }
}
