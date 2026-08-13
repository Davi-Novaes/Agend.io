using Agendio.Modules.Catalog.Contracts;
using Agendio.Modules.Resources.Domain;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.SetResourceServices;

public sealed class SetResourceServicesCommandHandler(ResourcesDbContext dbContext, IServiceLookupService serviceLookup)
    : ICommandHandler<SetResourceServicesCommand>
{
    public async Task<Result> Handle(SetResourceServicesCommand request, CancellationToken cancellationToken)
    {
        var resource = await dbContext.Resources
            .SingleOrDefaultAsync(r => r.Id == ResourceId.From(request.ResourceId), cancellationToken);

        if (resource is null)
        {
            return Result.Failure(Error.NotFound("Resource.NotFound", "Recurso nao encontrado."));
        }

        var distinctServiceIds = request.ServiceIds.Distinct().ToList();

        // IServiceLookupService ja restringe ao tenant corrente (query filter do
        // CatalogDbContext) — um Id de outro tenant simplesmente nao "existe"
        // aqui, mesmo raciocinio ja aplicado a Resource.UnitId via IUnitLookupService.
        foreach (var serviceId in distinctServiceIds)
        {
            var service = await serviceLookup.FindByIdAsync(serviceId, cancellationToken);
            if (service is null)
            {
                return Result.Failure(Error.Validation("Resource.ServiceNotFound", $"Servico {serviceId} nao encontrado."));
            }
        }

        resource.SetServiceIds(distinctServiceIds);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
