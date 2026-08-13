using Agendio.Modules.Resources.Domain;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.GetResourceById;

public sealed class GetResourceByIdQueryHandler(ResourcesDbContext dbContext) : IQueryHandler<GetResourceByIdQuery, ResourceDetails>
{
    public async Task<Result<ResourceDetails>> Handle(GetResourceByIdQuery request, CancellationToken cancellationToken)
    {
        // Colecoes owned (WorkingHours) sao carregadas automaticamente pelo EF
        // Core junto com o dono — nao precisam de Include explicito.
        var resource = await dbContext.Resources.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == ResourceId.From(request.ResourceId), cancellationToken);

        if (resource is null)
        {
            return Result.Failure<ResourceDetails>(Error.NotFound("Resource.NotFound", "Recurso nao encontrado."));
        }

        var workingHours = resource.WorkingHours
            .Select(w => new WorkingHourEntryDetails(w.DayOfWeek, w.StartTime, w.EndTime))
            .OrderBy(w => w.DayOfWeek)
            .ThenBy(w => w.StartTime)
            .ToList();

        var details = new ResourceDetails(
            resource.Id.Value, resource.Name, resource.Type, resource.Capacity, resource.Description, resource.IsActive, resource.UnitId,
            resource.PhotoUrl, resource.Specialties.ToList(), resource.ServiceIds.ToList(), workingHours);

        return Result.Success(details);
    }
}
