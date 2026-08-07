using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Billing.Domain;

public sealed record SubscriptionId(Guid Value) : TypedId(Value)
{
    public static SubscriptionId New() => new(Guid.NewGuid());

    public static SubscriptionId From(Guid value) => new(value);
}
