using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Scheduling.Domain;

public sealed record NotificationLogEntryId(Guid Value) : TypedId(Value)
{
    public static NotificationLogEntryId New() => new(Guid.NewGuid());

    public static NotificationLogEntryId From(Guid value) => new(value);
}
