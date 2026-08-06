using FluentValidation;

namespace Agendio.Modules.Identity.Application.RegisterUser;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(c => c.TenantId).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().MaximumLength(320);
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200);

        RuleFor(c => c.Password)
            .NotEmpty()
            .MinimumLength(10)
            .WithMessage("A senha precisa ter pelo menos 10 caracteres.");
    }
}
