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

        return new ResourceLookupResult(
            resource.Id.Value, resource.Name, resource.Type.ToString(), resource.Capacity, resource.IsActive, workingHours, resource.UnitId);
    }

    public async Task<IReadOnlyList<ResourceLookupResult>> ListActiveByTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ResourceType>(type, ignoreCase: true, out var parsedType))
        {
            return [];
        }

        var resources = await dbContext.Resources.AsNoTracking()
            .Where(r => r.Type == parsedType && r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return resources
            .Select(r => new ResourceLookupResult(
                r.Id.Value, r.Name, r.Type.ToString(), r.Capacity, r.IsActive,
                r.WorkingHours.Select(w => new WorkingHourLookup(w.DayOfWeek, w.StartTime, w.EndTime)).ToList(), r.UnitId))
            .ToList();
    }

    public async Task<IReadOnlyList<TimeOffLookup>> ListTimeOffAsync(
        Guid resourceId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        var resourceIdValue = ResourceId.From(resourceId);

        var timeOffs = await dbContext.TimeOffs.AsNoTracking()
            .Where(t => t.ResourceId == resourceIdValue && t.StartDate <= toDate && fromDate <= t.EndDate)
            .ToListAsync(cancellationToken);

        return timeOffs.Select(t => new TimeOffLookup(t.StartDate, t.EndDate)).ToList();
    }
}
