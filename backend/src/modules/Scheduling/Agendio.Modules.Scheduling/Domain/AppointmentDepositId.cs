using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Scheduling.Domain;

public sealed record AppointmentDepositId(Guid Value) : TypedId(Value)
{
    public static AppointmentDepositId New() => new(Guid.NewGuid());

    public static AppointmentDepositId From(Guid value) => new(value);
}
