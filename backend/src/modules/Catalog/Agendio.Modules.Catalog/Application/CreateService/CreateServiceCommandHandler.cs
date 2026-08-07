using Agendio.Modules.Catalog.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Catalog.Application.CreateService;

public sealed class CreateServiceCommandHandler(CatalogDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<CreateServiceCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateServiceCommand request, CancellationToken cancellationToken)
    {
        var serviceResult = Domain.Service.Create(
            tenantContext.TenantId, request.Name, request.Description, request.DurationMinutes,
            request.Price, request.Currency, request.Category);

        if (serviceResult.IsFailure)
        {
            return Result.Failure<Guid>(serviceResult.Error);
        }

        dbContext.Services.Add(serviceResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(serviceResult.Value.Id.Value);
    }
}
