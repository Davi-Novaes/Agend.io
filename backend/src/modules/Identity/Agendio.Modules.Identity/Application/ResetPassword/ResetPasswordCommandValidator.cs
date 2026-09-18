using Agendio.Modules.Identity.Application;
using FluentValidation;

namespace Agendio.Modules.Identity.Application.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty();

        RuleFor(c => c.NewPassword).RequireStrongPassword();
    }
}
