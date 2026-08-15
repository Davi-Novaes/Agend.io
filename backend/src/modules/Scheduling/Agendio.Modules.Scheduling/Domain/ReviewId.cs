using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Scheduling.Domain;

public sealed record ReviewId(Guid Value) : TypedId(Value)
{
    public static ReviewId New() => new(Guid.NewGuid());

    public static ReviewId From(Guid value) => new(value);
}
