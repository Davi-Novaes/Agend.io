using Agendio.Modules.Estoque.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Estoque.Application.CreateProduct;

public sealed class CreateProductCommandHandler(EstoqueDbContext dbContext, ITenantContext tenantContext)
    : ICommandHandler<CreateProductCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        Money? salePrice = null;
        if (request.SalePrice is not null)
        {
            var salePriceResult = Money.Create(request.SalePrice.Value, request.Currency ?? "BRL");
            if (salePriceResult.IsFailure)
            {
                return Result.Failure<Guid>(salePriceResult.Error);
            }

            salePrice = salePriceResult.Value;
        }

        var productResult = Domain.Product.Create(
            tenantContext.TenantId, request.Name, request.Sku, request.Description, request.QuantityInStock, request.MinimumStock,
            salePrice);

        if (productResult.IsFailure)
        {
            return Result.Failure<Guid>(productResult.Error);
        }

        dbContext.Products.Add(productResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(productResult.Value.Id.Value);
    }
}
