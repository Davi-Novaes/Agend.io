namespace Agendio.Infrastructure.Notifications;

/// <summary>
/// Envio real de mensagem via WhatsApp Cloud API (Meta) — nao um mock. As
/// credenciais (numero/token) sao POR TENANT, nunca globais (cada estabelecimento
/// conecta seu proprio WhatsApp Business), por isso entram como parametro em vez
/// de vir de IOptions (comparar com SmtpOptions/AsaasOptions, que sao globais).
/// </summary>
public interface IWhatsAppSender
{
    Task SendAsync(WhatsAppCredentials credentials, string toPhoneE164, string message, CancellationToken cancellationToken = default);
}

/// <summary>PhoneNumberId e o identificador do numero na Cloud API (nao e o proprio numero); AccessToken e o token de sistema/usuario com permissao whatsapp_business_messaging.</summary>
public sealed record WhatsAppCredentials(string PhoneNumberId, string AccessToken);
