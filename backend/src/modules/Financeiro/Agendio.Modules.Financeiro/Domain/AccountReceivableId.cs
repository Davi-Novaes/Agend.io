using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Financeiro.Domain;

public sealed record AccountReceivableId(Guid Value) : TypedId(Value)
{
    public static AccountReceivableId New() => new(Guid.NewGuid());

    public static AccountReceivableId From(Guid value) => new(value);
}
