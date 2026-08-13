using Agendio.Infrastructure.Storage;
using FluentValidation;

namespace Agendio.Modules.Resources.Application.UploadResourcePhoto;

public sealed class UploadResourcePhotoCommandValidator : AbstractValidator<UploadResourcePhotoCommand>
{
    private const int MaxSizeBytes = 2 * 1024 * 1024;

    public UploadResourcePhotoCommandValidator()
    {
        RuleFor(c => c.Content).NotEmpty();
        RuleFor(c => c.Content.Length)
            .LessThanOrEqualTo(MaxSizeBytes)
            .WithMessage("O arquivo nao pode ter mais que 2MB.");
        RuleFor(c => c.ContentType)
            .Must(ImageContentTypes.ExtensionByContentType.ContainsKey)
            .WithMessage("Formato invalido. Envie um arquivo PNG, JPEG ou WEBP.");
    }
}
