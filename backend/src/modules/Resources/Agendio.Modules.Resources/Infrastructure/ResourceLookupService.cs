using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Resources.Domain;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Infrastructure;

internal sealed class ResourceLookupService(ResourcesDbContext dbContext) : IResourceLookupService
{
    public async Task<ResourceLookupResult?> FindByIdAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var resource = await dbContext.Resources.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == ResourceId.From(resourceId), cancellationToken);

        if (resource is null)
        {
            return null;
        }

        var workingHours = resource.WorkingHours
            .Select(w => new WorkingHourLookup(w.DayOfWeek, w.StartTime, w.EndTime))
            .ToList();

        return new ResourceLookupResult(resource.Id.Value, resource.Name, resource.Type.ToString(), resource.Capacity, resource.IsActive, workingHours);
    }
}
