using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.CreateTenant;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Slug).NotEmpty().MaximumLength(63);
        RuleFor(c => c.BusinessType).IsInEnum();
        RuleFor(c => c.TimeZoneId).NotEmpty().MaximumLength(64);
    }
}
