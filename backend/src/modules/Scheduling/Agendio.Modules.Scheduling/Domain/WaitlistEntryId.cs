using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Scheduling.Domain;

public sealed record WaitlistEntryId(Guid Value) : TypedId(Value)
{
    public static WaitlistEntryId New() => new(Guid.NewGuid());

    public static WaitlistEntryId From(Guid value) => new(value);
}
