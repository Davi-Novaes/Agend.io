using FluentValidation;

namespace Agendio.Modules.Catalog.Application.UpdateService;

public sealed class UpdateServiceCommandValidator : AbstractValidator<UpdateServiceCommand>
{
    public UpdateServiceCommandValidator()
    {
        RuleFor(c => c.ServiceId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Description).MaximumLength(2000);
        RuleFor(c => c.DurationMinutes).GreaterThan(0);
        RuleFor(c => c.Price).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Currency).NotEmpty().Length(3);
        RuleFor(c => c.Category).MaximumLength(100);
    }
}
