using FluentValidation;

namespace Agendio.Modules.Platform.Application.DisablePlatformMfa;

public sealed class DisablePlatformMfaCommandValidator : AbstractValidator<DisablePlatformMfaCommand>
{
    public DisablePlatformMfaCommandValidator()
    {
        RuleFor(c => c.AdminId).NotEmpty();
        RuleFor(c => c.Password).NotEmpty();
        RuleFor(c => c.Code).NotEmpty().Length(6);
    }
}
