using Agendio.Infrastructure.Storage;
using Agendio.Modules.Catalog.Domain;
using Agendio.Modules.Catalog.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Catalog.Application.UploadServiceImage;

public sealed class UploadServiceImageCommandHandler(CatalogDbContext dbContext, IFileStorage fileStorage)
    : ICommandHandler<UploadServiceImageCommand, string>
{
    public async Task<Result<string>> Handle(UploadServiceImageCommand request, CancellationToken cancellationToken)
    {
        var service = await dbContext.Services.SingleOrDefaultAsync(s => s.Id == ServiceId.From(request.ServiceId), cancellationToken);
        if (service is null)
        {
            return Result.Failure<string>(Error.NotFound("Service.NotFound", "Servico nao encontrado."));
        }

        // Nome fixo por servico (nao por upload): reenviar SUBSTITUI a imagem
        // anterior em vez de acumular arquivo orfao a cada troca (mesmo padrao
        // de UpdateTenantLogoCommandHandler). ContentType ja validado pelo
        // pipeline (UploadServiceImageCommandValidator) — a chave sempre existe aqui.
        var extension = ImageContentTypes.ExtensionByContentType[request.ContentType];
        var relativePath = $"service-images/{request.ServiceId}{extension}";

        using var contentStream = new MemoryStream(request.Content);
        var imageUrl = await fileStorage.SaveAsync(relativePath, contentStream, cancellationToken);

        var setResult = service.SetImage(imageUrl);
        if (setResult.IsFailure)
        {
            return Result.Failure<string>(setResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(imageUrl);
    }
}
