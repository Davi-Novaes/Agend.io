using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantWhatsAppSettings;

public sealed class UpdateTenantWhatsAppSettingsCommandValidator : AbstractValidator<UpdateTenantWhatsAppSettingsCommand>
{
    public UpdateTenantWhatsAppSettingsCommandValidator()
    {
        RuleFor(c => c.PhoneNumberId).MaximumLength(64);
        RuleFor(c => c.ScheduledTemplate).MaximumLength(1000);
        RuleFor(c => c.ReminderTemplate).MaximumLength(1000);
        RuleFor(c => c.CancelledTemplate).MaximumLength(1000);
        RuleFor(c => c.RescheduledTemplate).MaximumLength(1000);
        RuleFor(c => c.ConfirmedTemplate).MaximumLength(1000);
        RuleFor(c => c.CompletedTemplate).MaximumLength(1000);
    }
}
