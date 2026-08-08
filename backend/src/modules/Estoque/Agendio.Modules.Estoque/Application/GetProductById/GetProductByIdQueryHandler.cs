using Agendio.Modules.Estoque.Domain;
using Agendio.Modules.Estoque.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Estoque.Application.GetProductById;

public sealed class GetProductByIdQueryHandler(EstoqueDbContext dbContext) : IQueryHandler<GetProductByIdQuery, ProductDetails>
{
    public async Task<Result<ProductDetails>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == ProductId.From(request.ProductId), cancellationToken);

        if (product is null)
        {
            return Result.Failure<ProductDetails>(Error.NotFound("Product.NotFound", "Produto nao encontrado."));
        }

        return Result.Success(new ProductDetails(
            product.Id.Value, product.Name, product.Sku, product.Description, product.QuantityInStock, product.MinimumStock,
            product.SalePrice?.Amount, product.SalePrice?.Currency, product.IsActive));
    }
}
