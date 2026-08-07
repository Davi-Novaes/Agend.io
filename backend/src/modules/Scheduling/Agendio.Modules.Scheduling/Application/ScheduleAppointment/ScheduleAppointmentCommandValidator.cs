using FluentValidation;

namespace Agendio.Modules.Scheduling.Application.ScheduleAppointment;

public sealed class ScheduleAppointmentCommandValidator : AbstractValidator<ScheduleAppointmentCommand>
{
    public ScheduleAppointmentCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.ResourceId).NotEmpty();
        RuleFor(c => c.ServiceId).NotEmpty();
        RuleFor(c => c.StartAtUtc).NotEmpty();
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}
