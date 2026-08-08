using Agendio.Modules.Estoque.Domain;
using Agendio.Modules.Estoque.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Estoque.Application.RegisterStockMovement;

public sealed class RegisterStockMovementCommandHandler(EstoqueDbContext dbContext, ITenantContext tenantContext, IClock clock)
    : ICommandHandler<RegisterStockMovementCommand, Guid>
{
    public async Task<Result<Guid>> Handle(RegisterStockMovementCommand request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(p => p.Id == ProductId.From(request.ProductId), cancellationToken);
        if (product is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Product.NotFound", "Produto nao encontrado."));
        }

        // Aplica no Product primeiro: se falhar (estoque insuficiente), nada e
        // persistido — nem o saldo muda nem a StockMovement nasce.
        var applyResult = product.ApplyMovement(request.Type, request.Quantity);
        if (applyResult.IsFailure)
        {
            return Result.Failure<Guid>(applyResult.Error);
        }

        var movementResult = Domain.StockMovement.Create(
            tenantContext.TenantId, request.ProductId, request.Type, request.Quantity, request.Reason, request.Notes,
            request.OccurredAtUtc ?? clock.UtcNow);

        if (movementResult.IsFailure)
        {
            return Result.Failure<Guid>(movementResult.Error);
        }

        dbContext.StockMovements.Add(movementResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(movementResult.Value.Id.Value);
    }
}
