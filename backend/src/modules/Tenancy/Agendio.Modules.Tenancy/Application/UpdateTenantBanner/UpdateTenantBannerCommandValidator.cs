using Agendio.Infrastructure.Storage;
using FluentValidation;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantBanner;

public sealed class UpdateTenantBannerCommandValidator : AbstractValidator<UpdateTenantBannerCommand>
{
    private const int MaxSizeBytes = 4 * 1024 * 1024;

    public UpdateTenantBannerCommandValidator()
    {
        RuleFor(c => c.Content).NotEmpty();
        RuleFor(c => c.Content.Length)
            .LessThanOrEqualTo(MaxSizeBytes)
            .WithMessage("O arquivo nao pode ter mais que 4MB.");
        // PNG/JPEG/WEBP: SVG fica de fora de proposito (pode carregar script embutido).
        RuleFor(c => c.ContentType)
            .Must(ImageContentTypes.ExtensionByContentType.ContainsKey)
            .WithMessage("Formato invalido. Envie um arquivo PNG, JPEG ou WEBP.");
    }
}
