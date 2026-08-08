using FluentValidation;

namespace Agendio.Modules.Identity.Application.EnableMfa;

public sealed class EnableMfaCommandValidator : AbstractValidator<EnableMfaCommand>
{
    public EnableMfaCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.Secret).NotEmpty();
        RuleFor(c => c.Code).NotEmpty().Length(6);
    }
}
