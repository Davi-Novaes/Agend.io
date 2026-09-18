using Agendio.Infrastructure.Security;
using Agendio.Modules.Platform.Domain;
using Agendio.Modules.Platform.Infrastructure.Mfa;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Platform.Application.DisablePlatformMfa;

public sealed class DisablePlatformMfaCommandHandler(
    PlatformDbContext dbContext, IPasswordHasher passwordHasher, IPlatformMfaCodeVerifier mfaCodeVerifier, SecurityAuditLogger<PlatformDbContext> securityAuditLogger)
    : ICommandHandler<DisablePlatformMfaCommand>
{
    public async Task<Result> Handle(DisablePlatformMfaCommand request, CancellationToken cancellationToken)
    {
        var admin = await dbContext.PlatformAdmins.SingleOrDefaultAsync(a => a.Id == PlatformAdminId.From(request.AdminId), cancellationToken);
        if (admin is null)
        {
            return Result.Failure(Error.NotFound("PlatformAdmin.NotFound", "Administrador nao encontrado."));
        }

        if (!admin.MfaEnabled)
        {
            return Result.Failure(Error.Validation("Mfa.NotEnabled", "MFA nao esta habilitado para este administrador."));
        }

        if (!passwordHasher.Verify(request.Password, admin.PasswordHash))
        {
            return Result.Failure(Error.Unauthorized("Auth.InvalidCredentials", "Senha invalida."));
        }

        if (!mfaCodeVerifier.Verify(admin, request.Code))
        {
            return Result.Failure(Error.Unauthorized("Auth.InvalidMfaCode", "Codigo invalido."));
        }

        admin.DisableMfa();
        await dbContext.SaveChangesAsync(cancellationToken);
        await securityAuditLogger.LogAsync("MfaDisabled", success: true, tenantId: null, admin.Id.Value, null, cancellationToken);

        return Result.Success();
    }
}
