using FluentValidation;

namespace Agendio.Modules.Billing.Application.SubscribeToPlan;

public sealed class SubscribeToPlanCommandValidator : AbstractValidator<SubscribeToPlanCommand>
{
    public SubscribeToPlanCommandValidator()
    {
        RuleFor(c => c.PlanId).NotEmpty();
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.CpfCnpj).NotEmpty().MaximumLength(20);
        RuleFor(c => c.Email).EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.Email));
    }
}
