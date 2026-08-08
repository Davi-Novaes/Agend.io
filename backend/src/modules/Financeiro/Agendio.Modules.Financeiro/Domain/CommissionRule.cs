using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Financeiro.Domain;

/// <summary>
/// Uma regra de comissao por profissional (Resource) — percentual ou valor fixo,
/// vale para todos os servicos que ele presta (sem granularidade por servico,
/// decisao deliberada para nao exigir configuracao complexa). ResourceId e Guid
/// puro: Financeiro nunca referencia Resources.Domain (ver CLAUDE.md), so
/// IResourceLookupService.Contracts para validar/exibir nome na Application layer.
/// Ausencia de regra (ou IsActive=false) para um recurso = sem comissao — a
/// funcionalidade fica invisivel ate o dono configurar.
/// </summary>
public sealed class CommissionRule : AggregateRoot<CommissionRuleId>, ITenantOwned
{
    public TenantId TenantId { get; private set; } = null!;

    public Guid ResourceId { get; private set; }

    public CommissionCalculationType CalculationType { get; private set; }

    public decimal Value { get; private set; }

    public bool IsActive { get; private set; }

    private CommissionRule()
    {
    }

    private CommissionRule(TenantId tenantId, Guid resourceId, CommissionCalculationType calculationType, decimal value)
        : base(CommissionRuleId.New())
    {
        TenantId = tenantId;
        ResourceId = resourceId;
        CalculationType = calculationType;
        Value = value;
        IsActive = true;
    }

    public static Result<CommissionRule> Create(TenantId tenantId, Guid resourceId, CommissionCalculationType calculationType, decimal value)
    {
        var validation = ValidateValue(calculationType, value);
        if (validation.IsFailure)
        {
            return Result.Failure<CommissionRule>(validation.Error);
        }

        return Result.Success(new CommissionRule(tenantId, resourceId, calculationType, value));
    }

    public Result UpdateRule(CommissionCalculationType calculationType, decimal value)
    {
        var validation = ValidateValue(calculationType, value);
        if (validation.IsFailure)
        {
            return validation;
        }

        CalculationType = calculationType;
        Value = value;
        return Result.Success();
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public Money CalculateCommission(Money servicePrice)
    {
        var amount = CalculationType == CommissionCalculationType.Percentage
            ? servicePrice.Amount * (Value / 100m)
            : Value;

        // Value ja validado (nao-negativo, percentual <= 100) em Create/UpdateRule
        // — Money.Create nunca falha aqui.
        return Money.Create(amount, servicePrice.Currency).Value;
    }

    private static Result ValidateValue(CommissionCalculationType calculationType, decimal value)
    {
        if (value < 0)
        {
            return Result.Failure(Error.Validation("CommissionRule.NegativeValue", "O valor da comissao nao pode ser negativo."));
        }

        if (calculationType == CommissionCalculationType.Percentage && value > 100)
        {
            return Result.Failure(Error.Validation("CommissionRule.InvalidPercentage", "O percentual de comissao nao pode ser maior que 100."));
        }

        return Result.Success();
    }
}
