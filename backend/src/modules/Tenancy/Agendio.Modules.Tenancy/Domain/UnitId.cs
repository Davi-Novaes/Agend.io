using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Tenancy.Domain;

public sealed record UnitId(Guid Value) : TypedId(Value)
{
    public static UnitId New() => new(Guid.NewGuid());

    public static UnitId From(Guid value) => new(value);
}
