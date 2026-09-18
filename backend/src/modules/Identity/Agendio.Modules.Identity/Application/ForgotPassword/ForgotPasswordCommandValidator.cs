using FluentValidation;

namespace Agendio.Modules.Identity.Application.ForgotPassword;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().MaximumLength(320);
    }
}
