using FluentValidation;

namespace Agendio.Modules.Platform.Application.VerifyPlatformMfa;

public sealed class VerifyPlatformMfaCommandValidator : AbstractValidator<VerifyPlatformMfaCommand>
{
    public VerifyPlatformMfaCommandValidator()
    {
        RuleFor(c => c.ChallengeToken).NotEmpty();
        RuleFor(c => c.Code).NotEmpty().Length(6);
    }
}
