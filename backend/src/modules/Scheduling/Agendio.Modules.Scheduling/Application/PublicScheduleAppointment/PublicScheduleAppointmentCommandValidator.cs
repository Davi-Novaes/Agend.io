using FluentValidation;

namespace Agendio.Modules.Scheduling.Application.PublicScheduleAppointment;

public sealed class PublicScheduleAppointmentCommandValidator : AbstractValidator<PublicScheduleAppointmentCommand>
{
    public PublicScheduleAppointmentCommandValidator()
    {
        RuleFor(c => c.ResourceId).NotEmpty();
        RuleFor(c => c.ServiceId).NotEmpty();
        RuleFor(c => c.StartAtUtc).NotEmpty();
        RuleFor(c => c.CustomerFullName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.CustomerEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(c => c.CustomerPhone).MaximumLength(30);
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}
