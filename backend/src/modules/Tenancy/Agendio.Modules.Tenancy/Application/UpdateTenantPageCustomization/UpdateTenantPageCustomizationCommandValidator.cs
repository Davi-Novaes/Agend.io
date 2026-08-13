using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantPageCustomization;

public sealed class UpdateTenantPageCustomizationCommandValidator : AbstractValidator<UpdateTenantPageCustomizationCommand>
{
    public UpdateTenantPageCustomizationCommandValidator()
    {
        RuleFor(c => c.SecondaryColorHex).MaximumLength(7);
    }
}
