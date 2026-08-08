using FluentValidation;

namespace Agendio.Modules.Identity.Application.VerifyMfa;

public sealed class VerifyMfaCommandValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaCommandValidator()
    {
        RuleFor(c => c.ChallengeToken).NotEmpty();
        RuleFor(c => c.Code).NotEmpty();
    }
}
