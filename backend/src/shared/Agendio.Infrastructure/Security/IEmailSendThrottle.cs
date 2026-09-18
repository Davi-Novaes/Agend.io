namespace Agendio.Infrastructure.Security;

/// <summary>
/// Limite de envio de e-mail por destinatario (nao por IP) — complementa o
/// rate limit de IP do endpoint (ver Program.cs, policy "auth"): sem isto, um
/// atacante trocando de IP consegue spammar reenvio de confirmacao/recuperacao
/// de senha para o MESMO destinatario indefinidamente (P1-7, docs/AUTH_BILLING_SECURITY_AUDIT.md).
/// </summary>
public interface IEmailSendThrottle
{
    /// <summary>Consome uma unidade da cota da janela e devolve se ainda havia cota — nunca lanca, so recusa.</summary>
    Task<bool> TryConsumeAsync(string key, int maxAttempts, TimeSpan window, CancellationToken cancellationToken);
}
