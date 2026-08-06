using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Identity.Domain;

public sealed record RefreshTokenId(Guid Value) : TypedId(Value)
{
    public static RefreshTokenId New() => new(Guid.NewGuid());

    public static RefreshTokenId From(Guid value) => new(value);
}
