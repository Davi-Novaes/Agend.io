using FluentValidation;

namespace Agendio.Modules.Resources.Application.CreateTimeOff;

public sealed class CreateTimeOffCommandValidator : AbstractValidator<CreateTimeOffCommand>
{
    public CreateTimeOffCommandValidator()
    {
        RuleFor(c => c.ResourceId).NotEmpty();
        RuleFor(c => c.Reason).MaximumLength(500);
    }
}
