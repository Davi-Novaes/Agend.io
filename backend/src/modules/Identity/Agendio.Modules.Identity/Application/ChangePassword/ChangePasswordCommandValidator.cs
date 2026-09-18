using Agendio.Modules.Identity.Application;
using FluentValidation;

namespace Agendio.Modules.Identity.Application.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.CurrentPassword).NotEmpty();

        RuleFor(c => c.NewPassword).RequireStrongPassword();
    }
}
