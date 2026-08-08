using FluentValidation;

namespace Agendio.Modules.Estoque.Application.RegisterStockMovement;

public sealed class RegisterStockMovementCommandValidator : AbstractValidator<RegisterStockMovementCommand>
{
    public RegisterStockMovementCommandValidator()
    {
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.Quantity).GreaterThan(0);
        RuleFor(c => c.Reason).IsInEnum();
        RuleFor(c => c.Notes).MaximumLength(500);
    }
}
