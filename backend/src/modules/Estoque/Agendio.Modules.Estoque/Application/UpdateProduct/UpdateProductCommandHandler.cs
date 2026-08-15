using Agendio.Modules.Estoque.Domain;
using Agendio.Modules.Estoque.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Estoque.Application.UpdateProduct;

public sealed class UpdateProductCommandHandler(EstoqueDbContext dbContext) : ICommandHandler<UpdateProductCommand>
{
    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(p => p.Id == ProductId.From(request.ProductId), cancellationToken);
        if (product is null)
        {
            return Result.Failure(Error.NotFound("Product.NotFound", "Produto nao encontrado."));
        }

        Money? costPrice = null;
        if (request.CostPrice is not null)
        {
            var costPriceResult = Money.Create(request.CostPrice.Value, request.Currency ?? "BRL");
            if (costPriceResult.IsFailure)
            {
                return Result.Failure(costPriceResult.Error);
            }

            costPrice = costPriceResult.Value;
        }

        Money? salePrice = null;
        if (request.SalePrice is not null)
        {
            var salePriceResult = Money.Create(request.SalePrice.Value, request.Currency ?? "BRL");
            if (salePriceResult.IsFailure)
            {
                return Result.Failure(salePriceResult.Error);
            }

            salePrice = salePriceResult.Value;
        }

        var updateResult = product.Update(request.Name, request.Sku, request.Category, request.Description, request.MinimumStock, costPrice, salePrice);
        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
