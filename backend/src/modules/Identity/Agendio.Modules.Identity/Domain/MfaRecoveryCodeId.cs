using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Identity.Domain;

public sealed record MfaRecoveryCodeId(Guid Value) : TypedId(Value)
{
    public static MfaRecoveryCodeId New() => new(Guid.NewGuid());

    public static MfaRecoveryCodeId From(Guid value) => new(value);
}
