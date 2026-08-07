using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Scheduling.Domain;

public sealed record AppointmentId(Guid Value) : TypedId(Value)
{
    public static AppointmentId New() => new(Guid.NewGuid());

    public static AppointmentId From(Guid value) => new(value);
}
