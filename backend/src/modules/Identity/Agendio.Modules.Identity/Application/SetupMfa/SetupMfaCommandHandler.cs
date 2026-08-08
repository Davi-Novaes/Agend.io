using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace Agendio.Modules.Identity.Application.SetupMfa;

/// <summary>Gera um secret novo e a URI de provisionamento (QR code) — nada e persistido ainda; so EnableMfa ativa de verdade.</summary>
public sealed class SetupMfaCommandHandler(IdentityDbContext dbContext) : ICommandHandler<SetupMfaCommand, SetupMfaResult>
{
    private const string Issuer = "Agendio";

    public async Task<Result<SetupMfaResult>> Handle(SetupMfaCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Id == UserId.From(request.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure<SetupMfaResult>(Error.NotFound("User.NotFound", "Usuario nao encontrado."));
        }

        if (user.MfaEnabled)
        {
            return Result.Failure<SetupMfaResult>(Error.Validation("Mfa.AlreadyEnabled", "MFA ja esta habilitado para este usuario."));
        }

        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secret = Base32Encoding.ToString(secretBytes);

        var otpAuthUri =
            $"otpauth://totp/{Issuer}:{Uri.EscapeDataString(user.Email.Value)}?secret={secret}&issuer={Issuer}&digits=6&period=30";

        return Result.Success(new SetupMfaResult(secret, otpAuthUri));
    }
}
