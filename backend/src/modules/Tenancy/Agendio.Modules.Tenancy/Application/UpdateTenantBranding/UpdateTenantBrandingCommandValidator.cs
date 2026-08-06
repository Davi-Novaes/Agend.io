using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantBranding;

public sealed class UpdateTenantBrandingCommandValidator : AbstractValidator<UpdateTenantBrandingCommand>
{
    public UpdateTenantBrandingCommandValidator()
    {
        RuleFor(c => c.PrimaryColorHex).NotEmpty().MaximumLength(7);
    }
}
