using FluentValidation;

namespace Agendio.Modules.Customers.Application.SendCustomerMessage;

public sealed class SendCustomerMessageCommandValidator : AbstractValidator<SendCustomerMessageCommand>
{
    public SendCustomerMessageCommandValidator()
    {
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(10000);
    }
}
