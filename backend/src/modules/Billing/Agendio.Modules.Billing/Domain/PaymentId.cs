using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Billing.Domain;

public sealed record PaymentId(Guid Value) : TypedId(Value)
{
    public static PaymentId New() => new(Guid.NewGuid());

    public static PaymentId From(Guid value) => new(value);
}
