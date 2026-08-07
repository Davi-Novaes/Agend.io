using FluentValidation;

namespace Agendio.Modules.Resources.Application.CreateResource;

public sealed class CreateResourceCommandValidator : AbstractValidator<CreateResourceCommand>
{
    public CreateResourceCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.Capacity).GreaterThan(0);
        RuleFor(c => c.Description).MaximumLength(2000);
    }
}
