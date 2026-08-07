using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Billing.Domain;

public sealed record PlanId(Guid Value) : TypedId(Value)
{
    public static PlanId New() => new(Guid.NewGuid());

    public static PlanId From(Guid value) => new(value);
}
