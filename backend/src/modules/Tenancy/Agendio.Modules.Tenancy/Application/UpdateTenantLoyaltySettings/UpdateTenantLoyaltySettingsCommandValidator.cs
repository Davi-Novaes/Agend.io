using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantLoyaltySettings;

public sealed class UpdateTenantLoyaltySettingsCommandValidator : AbstractValidator<UpdateTenantLoyaltySettingsCommand>
{
    public UpdateTenantLoyaltySettingsCommandValidator()
    {
        RuleFor(c => c.LoyaltyVisitsForReward).GreaterThan(0);
        RuleFor(c => c.LoyaltyRewardDescription).NotEmpty().MaximumLength(200);
    }
}
