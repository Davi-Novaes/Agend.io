using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Estoque.Application.ListProducts;

public sealed record ListProductsQuery(string? Search, bool? IsActive, bool LowStockOnly, int Page = 1, int PageSize = 20)
    : IQuery<ListProductsResult>;

public sealed record ProductSummary(
    Guid Id, string Name, string? Sku, int QuantityInStock, int MinimumStock, decimal? SalePrice, string? Currency, bool IsActive,
    bool IsLowStock);

public sealed record ListProductsResult(IReadOnlyList<ProductSummary> Items, int TotalCount, int Page, int PageSize);
