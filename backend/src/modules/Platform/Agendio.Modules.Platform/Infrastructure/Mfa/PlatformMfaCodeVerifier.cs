using Agendio.Modules.Platform.Domain;
using OtpNet;

namespace Agendio.Modules.Platform.Infrastructure.Mfa;

/// <summary>Sem codigo de recuperacao (ver comentario em PlatformAdmin.DisableMfa) — so verifica o TOTP.</summary>
public sealed class PlatformMfaCodeVerifier : IPlatformMfaCodeVerifier
{
    private static readonly VerificationWindow TotpWindow = new(previous: 1, future: 1);

    public bool Verify(PlatformAdmin admin, string code)
    {
        if (string.IsNullOrWhiteSpace(code) || admin.MfaSecretEncrypted is null)
        {
            return false;
        }

        // MfaSecretEncrypted ja chega DESCRIPTOGRAFADO aqui — ver comentario
        // equivalente em Identity.Infrastructure.Mfa.MfaCodeVerifier.
        var totp = new Totp(Base32Encoding.ToBytes(admin.MfaSecretEncrypted));
        return totp.VerifyTotp(code, out _, TotpWindow);
    }
}
