using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Estoque.Domain;

public sealed record ProductId(Guid Value) : TypedId(Value)
{
    public static ProductId New() => new(Guid.NewGuid());

    public static ProductId From(Guid value) => new(value);
}
