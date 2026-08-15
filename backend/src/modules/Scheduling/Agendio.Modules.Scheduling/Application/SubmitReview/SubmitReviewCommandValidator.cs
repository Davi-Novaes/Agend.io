using FluentValidation;

namespace Agendio.Modules.Scheduling.Application.SubmitReview;

public sealed class SubmitReviewCommandValidator : AbstractValidator<SubmitReviewCommand>
{
    public SubmitReviewCommandValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty();
        RuleFor(c => c.CustomerEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(c => c.Rating).InclusiveBetween(1, 5);
        RuleFor(c => c.Comment).MaximumLength(1000);
    }
}
