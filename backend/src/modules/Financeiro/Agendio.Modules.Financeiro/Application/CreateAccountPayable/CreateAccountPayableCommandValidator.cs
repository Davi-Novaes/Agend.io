using FluentValidation;

namespace Agendio.Modules.Financeiro.Application.CreateAccountPayable;

public sealed class CreateAccountPayableCommandValidator : AbstractValidator<CreateAccountPayableCommand>
{
    public CreateAccountPayableCommandValidator()
    {
        RuleFor(c => c.Description).NotEmpty().MaximumLength(500);
        RuleFor(c => c.Amount).GreaterThan(0);
        RuleFor(c => c.Category).IsInEnum();
    }
}
