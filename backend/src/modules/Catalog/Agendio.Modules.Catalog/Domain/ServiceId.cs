using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Catalog.Domain;

public sealed record ServiceId(Guid Value) : TypedId(Value)
{
    public static ServiceId New() => new(Guid.NewGuid());

    public static ServiceId From(Guid value) => new(value);
}
