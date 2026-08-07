using FluentValidation;

namespace Agendio.Modules.Platform.Application.LoginPlatformAdmin;

public sealed class LoginPlatformAdminCommandValidator : AbstractValidator<LoginPlatformAdminCommand>
{
    public LoginPlatformAdminCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
        RuleFor(c => c.Password).NotEmpty();
    }
}
