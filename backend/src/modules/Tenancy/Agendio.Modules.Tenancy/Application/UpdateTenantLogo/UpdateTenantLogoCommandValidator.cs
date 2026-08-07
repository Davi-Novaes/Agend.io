using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantLogo;

public sealed class UpdateTenantLogoCommandValidator : AbstractValidator<UpdateTenantLogoCommand>
{
    // PNG/JPEG/WEBP: SVG fica de fora de proposito (pode carregar script embutido).
    private static readonly HashSet<string> AllowedContentTypes = ["image/png", "image/jpeg", "image/webp"];
    private const int MaxSizeBytes = 2 * 1024 * 1024;

    public UpdateTenantLogoCommandValidator()
    {
        RuleFor(c => c.Content).NotEmpty();
        RuleFor(c => c.Content.Length)
            .LessThanOrEqualTo(MaxSizeBytes)
            .WithMessage("O arquivo nao pode ter mais que 2MB.");
        RuleFor(c => c.ContentType)
            .Must(AllowedContentTypes.Contains)
            .WithMessage("Formato invalido. Envie um arquivo PNG, JPEG ou WEBP.");
    }
}
