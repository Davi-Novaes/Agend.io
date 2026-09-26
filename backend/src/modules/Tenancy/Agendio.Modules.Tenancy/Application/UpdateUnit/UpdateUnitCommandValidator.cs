using Agendio.Modules.Tenancy.Domain;
using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.UpdateUnit;

public sealed class UpdateUnitCommandValidator : AbstractValidator<UpdateUnitCommand>
{
    public UpdateUnitCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Address).MaximumLength(500);
        RuleFor(c => c.City).MaximumLength(150);
        RuleFor(c => c.State)
            .Must(state => string.IsNullOrEmpty(state) || BrazilianStates.Codes.Contains(state))
            .WithMessage("Estado invalido. Informe a sigla de uma UF (ex.: SP).");
        RuleFor(c => c.Country).MaximumLength(100);
        RuleFor(c => c.Phone).MaximumLength(30);
        RuleFor(c => c.WhatsApp).MaximumLength(30);
    }
}
