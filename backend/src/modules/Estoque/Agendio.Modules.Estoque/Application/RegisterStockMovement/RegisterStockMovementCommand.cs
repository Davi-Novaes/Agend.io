using Agendio.Modules.Estoque.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Estoque.Application.RegisterStockMovement;

// OccurredAtUtc nulo = agora (IClock) — permite lancar uma movimentacao
// retroativa (ex.: compra da semana passada) quando informado explicitamente.
public sealed record RegisterStockMovementCommand(
    Guid ProductId, StockMovementType Type, int Quantity, StockMovementReason Reason, string? Notes, DateTimeOffset? OccurredAtUtc)
    : ICommand<Guid>;
