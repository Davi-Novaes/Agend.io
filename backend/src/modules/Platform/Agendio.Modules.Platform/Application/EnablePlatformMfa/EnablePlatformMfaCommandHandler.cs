using Agendio.Infrastructure.Security;
using Agendio.Modules.Platform.Domain;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace Agendio.Modules.Platform.Application.EnablePlatformMfa;

public sealed class EnablePlatformMfaCommandHandler(PlatformDbContext dbContext, SecurityAuditLogger<PlatformDbContext> securityAuditLogger)
    : ICommandHandler<EnablePlatformMfaCommand>
{
    private static readonly VerificationWindow TotpWindow = new(previous: 1, future: 1);

    public async Task<Result> Handle(EnablePlatformMfaCommand request, CancellationToken cancellationToken)
    {
        var admin = await dbContext.PlatformAdmins.SingleOrDefaultAsync(a => a.Id == PlatformAdminId.From(request.AdminId), cancellationToken);
        if (admin is null)
        {
            return Result.Failure(Error.NotFound("PlatformAdmin.NotFound", "Administrador nao encontrado."));
        }

        if (admin.MfaEnabled)
        {
            return Result.Failure(Error.Validation("Mfa.AlreadyEnabled", "MFA ja esta habilitado para este administrador."));
        }

        var totp = new Totp(Base32Encoding.ToBytes(request.Secret));
        if (!totp.VerifyTotp(request.Code, out _, TotpWindow))
        {
            return Result.Failure(Error.Validation("Mfa.InvalidCode", "Codigo invalido."));
        }

        admin.EnableMfa(request.Secret);
        await dbContext.SaveChangesAsync(cancellationToken);
        await securityAuditLogger.LogAsync("MfaEnabled", success: true, tenantId: null, admin.Id.Value, null, cancellationToken);

        return Result.Success();
    }
}
