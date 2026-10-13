using System.Security.Cryptography;
using Agendio.Infrastructure.Storage;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Tenancy.Application.UpdateTenantLogo;

public sealed class UpdateTenantLogoCommandHandler(
    TenancyDbContext dbContext, ITenantContext tenantContext, IFileStorage fileStorage)
    : ICommandHandler<UpdateTenantLogoCommand, string>
{
    public async Task<Result<string>> Handle(UpdateTenantLogoCommand request, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<string>(Error.NotFound("Tenant.NotFound", "Estabelecimento nao encontrado."));
        }

        // O arquivo continua fixo para nao acumular lixo, mas a URL recebe um
        // hash do conteudo. ContentType ja foi validado pelo pipeline.
        var extension = ImageContentTypes.ExtensionByContentType[request.ContentType];
        var relativePath = $"tenant-logos/{tenantContext.TenantId.Value}{extension}";

        using var contentStream = new MemoryStream(request.Content);
        var storedUrl = await fileStorage.SaveAsync(relativePath, contentStream, cancellationToken);
        var contentVersion = Convert.ToHexString(SHA256.HashData(request.Content))[..12].ToLowerInvariant();
        var logoUrl = $"{storedUrl}?v={contentVersion}";

        var setResult = tenant.SetLogo(logoUrl);
        if (setResult.IsFailure)
        {
            return Result.Failure<string>(setResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(logoUrl);
    }
}
