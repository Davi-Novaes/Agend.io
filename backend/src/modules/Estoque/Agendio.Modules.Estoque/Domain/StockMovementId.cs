using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Estoque.Domain;

public sealed record StockMovementId(Guid Value) : TypedId(Value)
{
    public static StockMovementId New() => new(Guid.NewGuid());

    public static StockMovementId From(Guid value) => new(value);
}
