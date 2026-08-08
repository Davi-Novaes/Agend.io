using Agendio.Infrastructure.Security;
using Agendio.Modules.Identity.Domain;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using OtpNet;

namespace Agendio.Modules.Identity.Infrastructure.Mfa;

public sealed class MfaCodeVerifier(IdentityDbContext dbContext, IRefreshTokenGenerator codeGenerator, IClock clock) : IMfaCodeVerifier
{
    private static readonly VerificationWindow TotpWindow = new(previous: 1, future: 1);

    public async Task<bool> VerifyAsync(User user, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        // MfaSecretEncrypted ja chega DESCRIPTOGRAFADO aqui: o EncryptedStringConverter
        // (aplicado em IdentityDbContext.OnModelCreating) atua na fronteira com o
        // banco — decrypt na leitura, encrypt na escrita — o mesmo raciocinio de
        // Customer.Cpf (ver docs/adr/0007). Chamar IEncryptionService.Decrypt aqui
        // de novo seria descriptografar um valor que ja esta em texto plano.
        if (IsTotpFormat(code) && user.MfaSecretEncrypted is not null)
        {
            var totp = new Totp(Base32Encoding.ToBytes(user.MfaSecretEncrypted));
            if (totp.VerifyTotp(code, out _, TotpWindow))
            {
                return true;
            }
        }

        return await TryConsumeRecoveryCodeAsync(user, code, cancellationToken);
    }

    private async Task<bool> TryConsumeRecoveryCodeAsync(User user, string code, CancellationToken cancellationToken)
    {
        var codeHash = codeGenerator.Hash(NormalizeRecoveryCode(code));

        var recoveryCode = await dbContext.MfaRecoveryCodes.SingleOrDefaultAsync(
            rc => rc.UserId == user.Id && rc.CodeHash == codeHash && rc.UsedAtUtc == null, cancellationToken);

        if (recoveryCode is null)
        {
            return false;
        }

        recoveryCode.MarkUsed(clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static bool IsTotpFormat(string code) => code.Length == 6 && code.All(char.IsAsciiDigit);

    private static string NormalizeRecoveryCode(string code) => code.Trim().ToUpperInvariant();
}
