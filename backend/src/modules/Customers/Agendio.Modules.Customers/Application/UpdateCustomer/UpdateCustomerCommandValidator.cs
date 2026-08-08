using FluentValidation;

namespace Agendio.Modules.Customers.Application.UpdateCustomer;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Notes).MaximumLength(2000);
        RuleFor(c => c.HealthNotes).MaximumLength(4000);
    }
}
