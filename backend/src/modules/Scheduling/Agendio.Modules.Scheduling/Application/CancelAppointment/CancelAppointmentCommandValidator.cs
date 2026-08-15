using FluentValidation;

namespace Agendio.Modules.Scheduling.Application.CancelAppointment;

public sealed class CancelAppointmentCommandValidator : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentCommandValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty();
        RuleFor(c => c.Reason).MaximumLength(500);
    }
}
