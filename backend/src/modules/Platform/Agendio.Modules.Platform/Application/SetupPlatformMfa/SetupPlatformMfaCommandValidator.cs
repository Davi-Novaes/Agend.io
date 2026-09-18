using FluentValidation;

namespace Agendio.Modules.Platform.Application.SetupPlatformMfa;

public sealed class SetupPlatformMfaCommandValidator : AbstractValidator<SetupPlatformMfaCommand>
{
    public SetupPlatformMfaCommandValidator()
    {
        RuleFor(c => c.AdminId).NotEmpty();
    }
}
