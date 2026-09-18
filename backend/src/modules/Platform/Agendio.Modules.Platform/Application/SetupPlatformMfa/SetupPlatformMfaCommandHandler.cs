using Agendio.Modules.Platform.Domain;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace Agendio.Modules.Platform.Application.SetupPlatformMfa;

/// <summary>Gera um secret novo e a URI de provisionamento (QR code) — nada e persistido ainda; so EnablePlatformMfa ativa de verdade.</summary>
public sealed class SetupPlatformMfaCommandHandler(PlatformDbContext dbContext) : ICommandHandler<SetupPlatformMfaCommand, SetupPlatformMfaResult>
{
    private const string Issuer = "Agendio Platform";

    public async Task<Result<SetupPlatformMfaResult>> Handle(SetupPlatformMfaCommand request, CancellationToken cancellationToken)
    {
        var admin = await dbContext.PlatformAdmins.SingleOrDefaultAsync(a => a.Id == PlatformAdminId.From(request.AdminId), cancellationToken);
        if (admin is null)
        {
            return Result.Failure<SetupPlatformMfaResult>(Error.NotFound("PlatformAdmin.NotFound", "Administrador nao encontrado."));
        }

        if (admin.MfaEnabled)
        {
            return Result.Failure<SetupPlatformMfaResult>(Error.Validation("Mfa.AlreadyEnabled", "MFA ja esta habilitado para este administrador."));
        }

        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretBytes);

        var otpAuthUri =
            $"otpauth://totp/{Issuer}:{Uri.EscapeDataString(admin.Email)}?secret={secret}&issuer={Uri.EscapeDataString(Issuer)}&digits=6&period=30";

        return Result.Success(new SetupPlatformMfaResult(secret, otpAuthUri));
    }
}
