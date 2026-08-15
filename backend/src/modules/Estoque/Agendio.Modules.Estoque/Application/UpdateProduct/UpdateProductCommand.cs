using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Estoque.Application.UpdateProduct;

// QuantityInStock nao entra aqui de proposito — so muda via RegisterStockMovement.
public sealed record UpdateProductCommand(
    Guid ProductId, string Name, string? Sku, string? Category, string? Description, int MinimumStock,
    decimal? CostPrice, decimal? SalePrice, string? Currency) : ICommand;
