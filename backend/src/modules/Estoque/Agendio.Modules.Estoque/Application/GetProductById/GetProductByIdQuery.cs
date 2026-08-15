using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Estoque.Application.GetProductById;

public sealed record GetProductByIdQuery(Guid ProductId) : IQuery<ProductDetails>;

public sealed record ProductDetails(
    Guid Id, string Name, string? Sku, string? Category, string? Description, int QuantityInStock, int MinimumStock,
    decimal? CostPrice, decimal? SalePrice, string? Currency, bool IsActive);
