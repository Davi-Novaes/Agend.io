using Agendio.Modules.Identity.Domain;

namespace Agendio.Modules.Identity.Infrastructure.Mfa;

/// <summary>Aceita tanto um codigo TOTP de 6 digitos quanto um codigo de recuperacao (uso unico, marcado como usado se aceito).</summary>
public interface IMfaCodeVerifier
{
    Task<bool> VerifyAsync(User user, string code, CancellationToken cancellationToken);
}
