using System.Text.Encodings.Web;
using Agendio.Infrastructure.Notifications;
using Microsoft.Extensions.Logging;

namespace Agendio.Modules.Customers.Infrastructure.Notifications;

/// <summary>
/// Job do Hangfire (durabilidade/retry sao do proprio Hangfire) — mesmo padrao
/// de CampaignEmailJob (Marketing), so que pra um unico destinatario avulso em
/// vez de uma campanha em massa (Fase 10 — recuperacao de clientes).
/// </summary>
public sealed class CustomerMessageEmailJob(IEmailSender emailSender, ILogger<CustomerMessageEmailJob> logger)
{
    public async Task SendAsync(
        Guid tenantId, string toEmail, string customerName, string subject, string bodyText, CancellationToken cancellationToken)
    {
        var normalizedBody = bodyText.Replace("\r\n", "\n");
        var encodedBody = HtmlEncoder.Default.Encode(normalizedBody).Replace("\n", "<br>");

        var html = $"""
            <p>Ola, {HtmlEncoder.Default.Encode(customerName)}!</p>
            <p>{encodedBody}</p>
            """;

        await emailSender.SendAsync(toEmail, subject, html, cancellationToken);
        logger.LogInformation("Mensagem avulsa enviada para {Email} (tenant {TenantId}).", toEmail, tenantId);
    }
}
