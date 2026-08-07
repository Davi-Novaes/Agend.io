using Agendio.Modules.Catalog.Domain;
using Agendio.Modules.Catalog.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Catalog.Application.GetServiceById;

public sealed class GetServiceByIdQueryHandler(CatalogDbContext dbContext) : IQueryHandler<GetServiceByIdQuery, ServiceDetails>
{
    public async Task<Result<ServiceDetails>> Handle(GetServiceByIdQuery request, CancellationToken cancellationToken)
    {
        var service = await dbContext.Services.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == ServiceId.From(request.ServiceId), cancellationToken);

        if (service is null)
        {
            return Result.Failure<ServiceDetails>(Error.NotFound("Service.NotFound", "Servico nao encontrado."));
        }

        var details = new ServiceDetails(
            service.Id.Value, service.Name, service.Description, service.DurationMinutes,
            service.Price.Amount, service.Price.Currency, service.Category, service.IsActive);

        return Result.Success(details);
    }
}
