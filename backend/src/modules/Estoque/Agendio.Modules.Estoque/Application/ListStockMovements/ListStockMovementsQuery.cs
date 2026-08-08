using Agendio.Modules.Estoque.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Estoque.Application.ListStockMovements;

public sealed record ListStockMovementsQuery(
    Guid? ProductId, StockMovementType? Type, StockMovementReason? Reason, DateOnly? From, DateOnly? To, int Page = 1, int PageSize = 20)
    : IQuery<ListStockMovementsResult>;

public sealed record StockMovementSummary(
    Guid Id, Guid ProductId, string ProductName, StockMovementType Type, int Quantity, StockMovementReason Reason, string? Notes,
    DateTimeOffset OccurredAtUtc);

public sealed record ListStockMovementsResult(IReadOnlyList<StockMovementSummary> Items, int TotalCount, int Page, int PageSize);
