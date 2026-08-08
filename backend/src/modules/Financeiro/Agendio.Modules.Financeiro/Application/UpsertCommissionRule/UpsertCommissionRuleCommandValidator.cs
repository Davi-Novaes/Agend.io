using FluentValidation;

namespace Agendio.Modules.Financeiro.Application.UpsertCommissionRule;

public sealed class UpsertCommissionRuleCommandValidator : AbstractValidator<UpsertCommissionRuleCommand>
{
    public UpsertCommissionRuleCommandValidator()
    {
        RuleFor(c => c.ResourceId).NotEmpty();
        RuleFor(c => c.CalculationType).IsInEnum();
        RuleFor(c => c.Value).GreaterThanOrEqualTo(0);
    }
}
