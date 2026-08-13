using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantSchedulingSettings;

public sealed class UpdateTenantSchedulingSettingsCommandValidator : AbstractValidator<UpdateTenantSchedulingSettingsCommand>
{
    public UpdateTenantSchedulingSettingsCommandValidator()
    {
        RuleForEach(c => c.ClosedDates).ChildRules(closedDate =>
        {
            closedDate.RuleFor(d => d.Reason).MaximumLength(200);
        });
    }
}
