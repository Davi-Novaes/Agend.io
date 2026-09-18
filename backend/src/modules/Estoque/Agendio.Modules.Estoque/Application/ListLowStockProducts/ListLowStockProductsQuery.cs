using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Estoque.Application.ListLowStockProducts;

public sealed record ListLowStockProductsQuery : IQuery<IReadOnlyList<LowStockProduct>>;

public sealed record LowStockProduct(Guid ProductId, string Name, int QuantityInStock, int MinimumStock);
