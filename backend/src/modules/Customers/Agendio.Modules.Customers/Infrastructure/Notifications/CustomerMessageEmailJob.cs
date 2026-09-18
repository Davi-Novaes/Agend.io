using System.Text.Encodings.Web;
using Agendio.Infrastructure;
using Agendio.Infrastructure.Notifications;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agendio.Modules.Customers.Infrastructure.Notifications;

/// <summary>
/// Job do Hangfire (durabilidade/retry sao do proprio Hangfire) — mesmo padrao
/// de CampaignEmailJob (Marketing), so que pra um unico destinatario avulso em
/// vez de uma campanha em massa (Fase 10 — recuperacao de clientes).
/// </summary>
public sealed class CustomerMessageEmailJob(
    IEmailSender emailSender,
    ITenantLookupService tenantLookup,
    IOptions<FrontendOptions> frontendOptions,
    ILogger<CustomerMessageEmailJob> logger)
{
    public async Task SendAsync(
        Guid tenantId, string toEmail, string customerName, string subject, string bodyText, CancellationToken cancellationToken)
    {
        var normalizedBody = bodyText.Replace("\r\n", "\n");
        var encodedBody = HtmlEncoder.Default.Encode(normalizedBody).Replace("\n", "<br>");

        // Mensagem avulsa (ex.: "sentimos sua falta") nao levava nenhum link pro
        // cliente agir — pedido do usuario apos ver o e-mail chegar sem forma de
        // marcar um horario. Mesmo padrao de link ja usado no e-mail de avaliacao
        // pos-atendimento (AppointmentNotificationJobs): {BaseUrl}/{tenant.Slug}.
        var tenant = await tenantLookup.FindByIdAsync(TenantId.From(tenantId), cancellationToken);
        var bookingLinkHtml = tenant is not null
            ? $"""<p><a href="{frontendOptions.Value.BaseUrl}/{tenant.Slug}">Agendar um horario</a></p>"""
            : string.Empty;

        var html = $"""
            <p>Ola, {HtmlEncoder.Default.Encode(customerName)}!</p>
            <p>{encodedBody}</p>
            {bookingLinkHtml}
            """;

        await emailSender.SendAsync(toEmail, subject, html, cancellationToken);
        logger.LogInformation("Mensagem avulsa enviada para {Email} (tenant {TenantId}).", PiiMasking.MaskEmail(toEmail), tenantId);
    }
}
