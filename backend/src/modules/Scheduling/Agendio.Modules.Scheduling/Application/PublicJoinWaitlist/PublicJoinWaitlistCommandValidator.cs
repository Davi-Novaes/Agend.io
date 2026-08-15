using FluentValidation;

namespace Agendio.Modules.Scheduling.Application.PublicJoinWaitlist;

public sealed class PublicJoinWaitlistCommandValidator : AbstractValidator<PublicJoinWaitlistCommand>
{
    public PublicJoinWaitlistCommandValidator()
    {
        RuleFor(c => c.ServiceId).NotEmpty();
        RuleFor(c => c.PreferredDate).NotEmpty();
        RuleFor(c => c.CustomerFullName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.CustomerEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(c => c.CustomerPhone).MaximumLength(30);
        RuleFor(c => c.Notes).MaximumLength(500);
    }
}
