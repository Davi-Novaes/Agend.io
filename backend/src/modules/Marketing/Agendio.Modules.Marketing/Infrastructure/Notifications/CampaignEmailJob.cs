using System.Text.Encodings.Web;
using Agendio.Infrastructure.Notifications;
using Microsoft.Extensions.Logging;

namespace Agendio.Modules.Marketing.Infrastructure.Notifications;

/// <summary>
/// Cada metodo publico e um job do Hangfire (durabilidade e retry automatico
/// sao do proprio Hangfire, ver AppointmentNotificationJobs para o mesmo
/// padrao). Mais simples que AppointmentNotificationJobs: nao precisa de
/// DbContext/ITenantContext.SetTenant porque nao ha nada para "ficar obsoleto"
/// entre o enqueue e a execucao — assunto/corpo de uma campanha ja disparada
/// nunca mudam, e o destinatario ja foi resolvido no momento do envio.
/// </summary>
public sealed class CampaignEmailJob(IEmailSender emailSender, ILogger<CampaignEmailJob> logger)
{
    public async Task SendAsync(
        Guid tenantId, string toEmail, string customerName, string subject, string bodyText, CancellationToken cancellationToken)
    {
        // Texto livre digitado pelo dono — nunca interpolado como HTML cru.
        // Encode primeiro, troca de quebra de linha depois (senao o <br> vira escapado).
        var normalizedBody = bodyText.Replace("\r\n", "\n");
        var encodedBody = HtmlEncoder.Default.Encode(normalizedBody).Replace("\n", "<br>");

        var html = $"""
            <p>Ola, {HtmlEncoder.Default.Encode(customerName)}!</p>
            <p>{encodedBody}</p>
            """;

        await emailSender.SendAsync(toEmail, subject, html, cancellationToken);
        logger.LogInformation("Campanha enviada para {Email} (tenant {TenantId}).", PiiMasking.MaskEmail(toEmail), tenantId);
    }
}
