using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Estoque.Application.UpdateProduct;

// QuantityInStock nao entra aqui de proposito — so muda via RegisterStockMovement.
public sealed record UpdateProductCommand(
    Guid ProductId, string Name, string? Sku, string? Description, int MinimumStock, decimal? SalePrice, string? Currency) : ICommand;
