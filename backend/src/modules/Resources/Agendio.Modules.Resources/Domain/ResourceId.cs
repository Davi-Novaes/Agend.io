using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Resources.Domain;

public sealed record ResourceId(Guid Value) : TypedId(Value)
{
    public static ResourceId New() => new(Guid.NewGuid());

    public static ResourceId From(Guid value) => new(value);
}
