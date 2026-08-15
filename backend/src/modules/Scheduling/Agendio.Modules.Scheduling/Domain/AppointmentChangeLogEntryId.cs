using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Scheduling.Domain;

public sealed record AppointmentChangeLogEntryId(Guid Value) : TypedId(Value)
{
    public static AppointmentChangeLogEntryId New() => new(Guid.NewGuid());

    public static AppointmentChangeLogEntryId From(Guid value) => new(value);
}
