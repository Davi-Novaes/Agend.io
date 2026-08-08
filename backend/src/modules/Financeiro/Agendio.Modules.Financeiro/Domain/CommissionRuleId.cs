using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Financeiro.Domain;

public sealed record CommissionRuleId(Guid Value) : TypedId(Value)
{
    public static CommissionRuleId New() => new(Guid.NewGuid());

    public static CommissionRuleId From(Guid value) => new(value);
}
