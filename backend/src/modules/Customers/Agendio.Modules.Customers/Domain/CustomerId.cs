using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Customers.Domain;

public sealed record CustomerId(Guid Value) : TypedId(Value)
{
    public static CustomerId New() => new(Guid.NewGuid());

    public static CustomerId From(Guid value) => new(value);
}
