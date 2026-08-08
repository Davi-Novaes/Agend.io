using FluentValidation;

namespace Agendio.Modules.Customers.Application.CreateCustomer;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Notes).MaximumLength(2000);
        RuleFor(c => c.HealthNotes).MaximumLength(4000);
    }
}
