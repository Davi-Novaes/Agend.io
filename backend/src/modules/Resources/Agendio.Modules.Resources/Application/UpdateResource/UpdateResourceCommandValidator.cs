using FluentValidation;

namespace Agendio.Modules.Resources.Application.UpdateResource;

public sealed class UpdateResourceCommandValidator : AbstractValidator<UpdateResourceCommand>
{
    public UpdateResourceCommandValidator()
    {
        RuleFor(c => c.ResourceId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.Capacity).GreaterThan(0);
        RuleFor(c => c.Description).MaximumLength(2000);
    }
}
