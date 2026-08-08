using FluentValidation;

namespace Agendio.Modules.Identity.Application.DisableMfa;

public sealed class DisableMfaCommandValidator : AbstractValidator<DisableMfaCommand>
{
    public DisableMfaCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Password).NotEmpty();
        RuleFor(c => c.Code).NotEmpty();
    }
}
