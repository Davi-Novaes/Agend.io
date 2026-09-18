using Agendio.Modules.Platform.Domain;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Platform.Application.GetPlatformMfaStatus;

public sealed class GetPlatformMfaStatusQueryHandler(PlatformDbContext dbContext) : IQueryHandler<GetPlatformMfaStatusQuery, PlatformMfaStatusResult>
{
    public async Task<Result<PlatformMfaStatusResult>> Handle(GetPlatformMfaStatusQuery request, CancellationToken cancellationToken)
    {
        var admin = await dbContext.PlatformAdmins.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == PlatformAdminId.From(request.AdminId), cancellationToken);

        if (admin is null)
        {
            return Result.Failure<PlatformMfaStatusResult>(Error.NotFound("PlatformAdmin.NotFound", "Administrador nao encontrado."));
        }

        return Result.Success(new PlatformMfaStatusResult(admin.MfaEnabled));
    }
}
