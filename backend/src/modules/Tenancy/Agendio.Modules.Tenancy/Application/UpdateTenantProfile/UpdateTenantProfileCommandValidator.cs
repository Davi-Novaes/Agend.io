using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantProfile;

public sealed class UpdateTenantProfileCommandValidator : AbstractValidator<UpdateTenantProfileCommand>
{
    public UpdateTenantProfileCommandValidator()
    {
        RuleFor(c => c.Description).MaximumLength(2000);
        RuleFor(c => c.Phone).MaximumLength(20);
        RuleFor(c => c.WhatsApp).MaximumLength(20);
        RuleFor(c => c.Email).MaximumLength(320);
        RuleFor(c => c.Address).MaximumLength(500);
        RuleFor(c => c.InstagramUrl).MaximumLength(500);
        RuleFor(c => c.FacebookUrl).MaximumLength(500);
    }
}
