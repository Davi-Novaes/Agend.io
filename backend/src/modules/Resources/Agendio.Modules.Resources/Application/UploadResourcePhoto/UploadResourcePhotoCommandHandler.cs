using Agendio.Infrastructure.Storage;
using Agendio.Modules.Resources.Domain;
using Agendio.Modules.Resources.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Resources.Application.UploadResourcePhoto;

public sealed class UploadResourcePhotoCommandHandler(ResourcesDbContext dbContext, IFileStorage fileStorage)
    : ICommandHandler<UploadResourcePhotoCommand, string>
{
    public async Task<Result<string>> Handle(UploadResourcePhotoCommand request, CancellationToken cancellationToken)
    {
        var resource = await dbContext.Resources
            .SingleOrDefaultAsync(r => r.Id == ResourceId.From(request.ResourceId), cancellationToken);
        if (resource is null)
        {
            return Result.Failure<string>(Error.NotFound("Resource.NotFound", "Recurso nao encontrado."));
        }

        // Nome fixo por recurso (nao por upload): reenviar SUBSTITUI a foto
        // anterior em vez de acumular arquivo orfao a cada troca. ContentType ja
        // validado pelo pipeline (UploadResourcePhotoCommandValidator).
        var extension = ImageContentTypes.ExtensionByContentType[request.ContentType];
        var relativePath = $"resource-photos/{request.ResourceId}{extension}";

        using var contentStream = new MemoryStream(request.Content);
        var photoUrl = await fileStorage.SaveAsync(relativePath, contentStream, cancellationToken);

        var setResult = resource.SetPhoto(photoUrl);
        if (setResult.IsFailure)
        {
            return Result.Failure<string>(setResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(photoUrl);
    }
}
