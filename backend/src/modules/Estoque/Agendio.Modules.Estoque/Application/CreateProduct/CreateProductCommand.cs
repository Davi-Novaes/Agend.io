using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Estoque.Application.CreateProduct;

// Sem TenantId: vem de ITenantContext (claim do JWT).
public sealed record CreateProductCommand(
    string Name, string? Sku, string? Description, int QuantityInStock, int MinimumStock, decimal? SalePrice, string? Currency)
    : ICommand<Guid>;
