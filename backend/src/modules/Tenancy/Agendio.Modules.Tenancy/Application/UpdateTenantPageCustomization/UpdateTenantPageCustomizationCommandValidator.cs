using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantPageCustomization;

public sealed class UpdateTenantPageCustomizationCommandValidator : AbstractValidator<UpdateTenantPageCustomizationCommand>
{
    public UpdateTenantPageCustomizationCommandValidator()
    {
        RuleFor(c => c.SecondaryColorHex).MaximumLength(7);
        RuleFor(c => c.HomeHeroTitle).MaximumLength(200);
        RuleFor(c => c.HomeHeroDescription).MaximumLength(1000);
        RuleFor(c => c.HomeCtaText).MaximumLength(60);
        RuleFor(c => c.BookingInstructionsText).MaximumLength(1000);
    }
}
