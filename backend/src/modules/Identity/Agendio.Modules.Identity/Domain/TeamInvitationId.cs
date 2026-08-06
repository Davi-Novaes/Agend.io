using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Identity.Domain;

public sealed record TeamInvitationId(Guid Value) : TypedId(Value)
{
    public static TeamInvitationId New() => new(Guid.NewGuid());

    public static TeamInvitationId From(Guid value) => new(value);
}
