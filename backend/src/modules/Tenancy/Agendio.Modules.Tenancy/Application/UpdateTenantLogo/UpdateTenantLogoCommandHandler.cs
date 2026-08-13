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

        // Nome fixo por tenant (nao por upload): reenviar SUBSTITUI o logo
        // anterior em vez de acumular arquivo orfao a cada troca. ContentType ja
        // validado pelo pipeline (UpdateTenantLogoCommandValidator).
        var extension = ImageContentTypes.ExtensionByContentType[request.ContentType];
        var relativePath = $"tenant-logos/{tenantContext.TenantId.Value}{extension}";

        using var contentStream = new MemoryStream(request.Content);
        var logoUrl = await fileStorage.SaveAsync(relativePath, contentStream, cancellationToken);

        var setResult = tenant.SetLogo(logoUrl);
        if (setResult.IsFailure)
        {
            return Result.Failure<string>(setResult.Error);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(logoUrl);
    }
}
