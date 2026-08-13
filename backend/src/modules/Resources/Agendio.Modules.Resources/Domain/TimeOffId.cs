using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Resources.Domain;

public sealed record TimeOffId(Guid Value) : TypedId(Value)
{
    public static TimeOffId New() => new(Guid.NewGuid());

    public static TimeOffId From(Guid value) => new(value);
}
