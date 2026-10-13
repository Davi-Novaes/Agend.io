using System.Security.Cryptography;
using Agendio.Infrastructure.Storage;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantBanner;

public sealed class UpdateTenantBannerCommandHandler(
    TenancyDbContext dbContext, ITenantContext tenantContext, IFileStorage fileStorage)
    : ICommandHandler<UpdateTenantBannerCommand, string>
{
    public async Task<Result<string>> Handle(UpdateTenantBannerCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<string>(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        // O arquivo continua fixo para nao acumular lixo, mas a URL recebe um
        // hash do conteudo. Assim navegador, Next e CDN buscam a imagem nova.
        var extension = ImageContentTypes.ExtensionByContentType[request.ContentType];
        var relativePath = $"tenant-banners/{tenantContext.TenantId.Value}{extension}";

        using var contentStream = new MemoryStream(request.Content);
        var storedUrl = await fileStorage.SaveAsync(relativePath, contentStream, cancellationToken);
        var contentVersion = Convert.ToHexString(SHA256.HashData(request.Content))[..12].ToLowerInvariant();
        var bannerUrl = $"{storedUrl}?v={contentVersion}";

        var setResult = tenant.SetBanner(bannerUrl);
        if (setResult.IsFailure)
        {
            return Result.Failure<string>(setResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(bannerUrl);
    }
}
