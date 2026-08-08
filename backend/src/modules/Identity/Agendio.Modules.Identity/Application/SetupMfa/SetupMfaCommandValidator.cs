using FluentValidation;

namespace Agendio.Modules.Identity.Application.SetupMfa;

public sealed class SetupMfaCommandValidator : AbstractValidator<SetupMfaCommand>
{
    public SetupMfaCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
    }
}
