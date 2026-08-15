using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantNoShowPolicy;

public sealed class UpdateTenantNoShowPolicyCommandValidator : AbstractValidator<UpdateTenantNoShowPolicyCommand>
{
    public UpdateTenantNoShowPolicyCommandValidator()
    {
        RuleFor(c => c.NoShowThresholdForDeposit).GreaterThan(0);
    }
}
