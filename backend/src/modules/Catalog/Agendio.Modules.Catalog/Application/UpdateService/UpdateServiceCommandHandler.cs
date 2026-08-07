using Agendio.Modules.Catalog.Domain;
using Agendio.Modules.Catalog.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Catalog.Application.UpdateService;

public sealed class UpdateServiceCommandHandler(CatalogDbContext dbContext) : ICommandHandler<UpdateServiceCommand>
{
    public async Task<Result> Handle(UpdateServiceCommand request, CancellationToken cancellationToken)
    {
        var service = await dbContext.Services
            .SingleOrDefaultAsync(s => s.Id == ServiceId.From(request.ServiceId), cancellationToken);

        if (service is null)
        {
            return Result.Failure(Error.NotFound("Service.NotFound", "Servico nao encontrado."));
        }

        var updateResult = service.Update(
            request.Name, request.Description, request.DurationMinutes, request.Price, request.Currency, request.Category);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
