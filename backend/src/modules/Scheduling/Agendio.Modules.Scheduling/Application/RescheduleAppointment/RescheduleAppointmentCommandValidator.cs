using FluentValidation;

namespace Agendio.Modules.Scheduling.Application.RescheduleAppointment;

public sealed class RescheduleAppointmentCommandValidator : AbstractValidator<RescheduleAppointmentCommand>
{
    public RescheduleAppointmentCommandValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty();
        RuleFor(c => c.NewStartAtUtc).NotEmpty();
        RuleFor(c => c.Reason).MaximumLength(500);
        // Mesmo piso de 30min do grid da Agenda (SLOT_MINUTES no frontend) —
        // redimensionar abaixo disso nao faz sentido visual nem operacional.
        RuleFor(c => c.NewDurationMinutes)
            .GreaterThanOrEqualTo(30)
            .When(c => c.NewDurationMinutes is not null);
        RuleFor(c => c.NewResourceId).NotEmpty().When(c => c.NewResourceId is not null);
    }
}
