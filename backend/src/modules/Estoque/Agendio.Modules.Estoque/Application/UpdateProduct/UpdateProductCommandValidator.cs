using FluentValidation;

namespace Agendio.Modules.Estoque.Application.UpdateProduct;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
        RuleFor(c => c.Sku).MaximumLength(50);
        RuleFor(c => c.Category).MaximumLength(100);
        RuleFor(c => c.Description).MaximumLength(1000);
        RuleFor(c => c.MinimumStock).GreaterThanOrEqualTo(0);
        RuleFor(c => c.CostPrice).GreaterThan(0).When(c => c.CostPrice is not null);
        RuleFor(c => c.SalePrice).GreaterThan(0).When(c => c.SalePrice is not null);
    }
}
