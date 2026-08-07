namespace Agendio.Infrastructure.Notifications;

/// <summary>
/// Capacidade minima de envio de e-mail transacional. NAO e o modulo
/// Notifications completo do roadmap (Sprint 5) — aquele vai cobrir
/// lembrete/confirmacao/template versionado. Isto e so o suficiente para o
/// convite de equipe funcionar de verdade em vez de exigir copiar um link.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
