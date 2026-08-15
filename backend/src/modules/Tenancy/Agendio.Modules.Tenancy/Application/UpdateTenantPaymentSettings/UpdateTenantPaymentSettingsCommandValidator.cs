using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantPaymentSettings;

public sealed class UpdateTenantPaymentSettingsCommandValidator : AbstractValidator<UpdateTenantPaymentSettingsCommand>
{
    public UpdateTenantPaymentSettingsCommandValidator()
    {
        RuleFor(c => c.DepositPercentage).InclusiveBetween(1, 100);
    }
}
