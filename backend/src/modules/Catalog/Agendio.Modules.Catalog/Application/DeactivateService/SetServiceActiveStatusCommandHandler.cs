using Agendio.Modules.Catalog.Domain;
using Agendio.Modules.Catalog.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Catalog.Application.DeactivateService;

public sealed class SetServiceActiveStatusCommandHandler(CatalogDbContext dbContext) : ICommandHandler<SetServiceActiveStatusCommand>
{
    public async Task<Result> Handle(SetServiceActiveStatusCommand request, CancellationToken cancellationToken)
    {
        var service = await dbContext.Services
            .SingleOrDefaultAsync(s => s.Id == ServiceId.From(request.ServiceId), cancellationToken);

        if (service is null)
        {
            return Result.Failure(Error.NotFound("Service.NotFound", "Servico nao encontrado."));
        }

        if (request.IsActive)
        {
            service.Activate();
        }
        else
        {
            service.Deactivate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
