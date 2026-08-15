using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Customers.Domain;

public sealed record LoyaltyPointsLedgerEntryId(Guid Value) : TypedId(Value)
{
    public static LoyaltyPointsLedgerEntryId New() => new(Guid.NewGuid());

    public static LoyaltyPointsLedgerEntryId From(Guid value) => new(value);
}
