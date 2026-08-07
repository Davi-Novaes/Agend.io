using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Platform.Domain;

public sealed record PlatformAdminId(Guid Value) : TypedId(Value)
{
    public static PlatformAdminId New() => new(Guid.NewGuid());

    public static PlatformAdminId From(Guid value) => new(value);
}
