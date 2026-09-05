using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantCompanyInfo;

public sealed class UpdateTenantCompanyInfoCommandValidator : AbstractValidator<UpdateTenantCompanyInfoCommand>
{
    public UpdateTenantCompanyInfoCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.LegalName).MaximumLength(200);
        RuleFor(c => c.Document).MaximumLength(18);
        RuleFor(c => c.City).MaximumLength(120);
        RuleFor(c => c.State).MaximumLength(2);
        RuleFor(c => c.ZipCode).MaximumLength(9);
    }
}
