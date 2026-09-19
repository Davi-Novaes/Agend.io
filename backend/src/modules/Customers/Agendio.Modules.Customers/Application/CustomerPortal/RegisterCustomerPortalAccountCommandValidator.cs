using FluentValidation;

namespace Agendio.Modules.Customers.Application.CustomerPortal;

public sealed class RegisterCustomerPortalAccountCommandValidator : AbstractValidator<RegisterCustomerPortalAccountCommand>
{
    public RegisterCustomerPortalAccountCommandValidator()
    {
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(c => c.Phone).MaximumLength(30);
    }
}
