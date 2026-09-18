using Agendio.Infrastructure.Storage;
using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Application.UploadUserAvatar;

public sealed class UploadUserAvatarCommandHandler(IdentityDbContext dbContext, IFileStorage fileStorage)
    : ICommandHandler<UploadUserAvatarCommand, string>
{
    public async Task<Result<string>> Handle(UploadUserAvatarCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure<string>(Error.NotFound("User.NotFound", "Usuario nao encontrado."));
        }

        // Nome fixo por usuario (nao por upload): reenviar SUBSTITUI a foto
        // anterior em vez de acumular arquivo orfao a cada troca (mesmo padrao
        // de UploadResourcePhotoCommandHandler/UploadServiceImageCommandHandler).
        var extension = ImageContentTypes.ExtensionByContentType[request.ContentType];
        var relativePath = $"user-avatars/{request.UserId}{extension}";

        using var contentStream = new MemoryStream(request.Content);
        var avatarUrl = await fileStorage.SaveAsync(relativePath, contentStream, cancellationToken);

        var setResult = user.SetAvatar(avatarUrl);
        if (setResult.IsFailure)
        {
            return Result.Failure<string>(setResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(avatarUrl);
    }
}
