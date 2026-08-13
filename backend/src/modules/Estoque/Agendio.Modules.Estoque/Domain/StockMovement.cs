using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Estoque.Domain;

/// <summary>
/// Registro imutavel de uma entrada ou saida de estoque — nunca editado ou
/// apagado depois de criado (log de auditoria). OccurredAtUtc e a data de
/// negocio da movimentacao (pode ser retroativa); CreatedAtUtc (IAuditable) e
/// quando o registro foi de fato inserido.
/// </summary>
public sealed class StockMovement : AggregateRoot<StockMovementId>, ITenantOwned, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public ProductId ProductId { get; private set; } = null!;

    public StockMovementType Type { get; private set; }

    public int Quantity { get; private set; }

    public StockMovementReason Reason { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private StockMovement()
    {
    }

    private StockMovement(
        TenantId tenantId, ProductId productId, StockMovementType type, int quantity, StockMovementReason reason, string? notes,
        DateTimeOffset occurredAtUtc)
        : base(StockMovementId.New())
    {
        TenantId = tenantId;
        ProductId = productId;
        Type = type;
        Quantity = quantity;
        Reason = reason;
        Notes = notes;
        OccurredAtUtc = occurredAtUtc;
    }

    public static Result<StockMovement> Create(
        TenantId tenantId, ProductId productId, StockMovementType type, int quantity, StockMovementReason reason, string? notes,
        DateTimeOffset occurredAtUtc)
    {
        if (quantity <= 0)
        {
            return Result.Failure<StockMovement>(
                Error.Validation("StockMovement.InvalidQuantity", "A quantidade da movimentacao precisa ser maior que zero."));
        }

        return Result.Success(new StockMovement(
            tenantId, productId, type, quantity, reason, string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), occurredAtUtc));
    }
}
