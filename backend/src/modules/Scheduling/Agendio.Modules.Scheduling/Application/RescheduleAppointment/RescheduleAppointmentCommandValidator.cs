using FluentValidation;

namespace Agendio.Modules.Scheduling.Application.RescheduleAppointment;

public sealed class RescheduleAppointmentCommandValidator : AbstractValidator<RescheduleAppointmentCommand>
{
    public RescheduleAppointmentCommandValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty();
        RuleFor(c => c.NewStartAtUtc).NotEmpty();
        RuleFor(c => c.Reason).MaximumLength(500);
    }
}
