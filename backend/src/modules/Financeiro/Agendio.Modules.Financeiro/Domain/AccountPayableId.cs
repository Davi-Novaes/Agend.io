using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Financeiro.Domain;

public sealed record AccountPayableId(Guid Value) : TypedId(Value)
{
    public static AccountPayableId New() => new(Guid.NewGuid());

    public static AccountPayableId From(Guid value) => new(value);
}
