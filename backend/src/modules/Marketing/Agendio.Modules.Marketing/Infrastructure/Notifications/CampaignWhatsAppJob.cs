using Agendio.Infrastructure.Notifications;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.Extensions.Logging;

namespace Agendio.Modules.Marketing.Infrastructure.Notifications;

/// <summary>
/// Mesmo padrao de CampaignEmailJob: cada metodo publico e um job do Hangfire.
/// Credenciais sao resolvidas AQUI, no momento do envio (nao passadas como
/// argumento do job) — mesmo motivo de AppointmentNotificationJobs: evita
/// guardar token de acesso na tabela de jobs do Hangfire, e tolera a
/// configuracao mudar entre o enfileiramento e a execucao.
/// </summary>
public sealed class CampaignWhatsAppJob(ITenantLookupService tenantLookup, IWhatsAppSender whatsAppSender, ILogger<CampaignWhatsAppJob> logger)
{
    public async Task SendAsync(Guid tenantId, string toPhoneE164, string customerName, string message, CancellationToken cancellationToken)
    {
        var settings = await tenantLookup.GetWhatsAppSettingsAsync(TenantId.From(tenantId), cancellationToken);
        if (settings is null || !settings.Enabled || settings.PhoneNumberId is null || settings.AccessToken is null)
        {
            logger.LogWarning(
                "Campanha WhatsApp nao enviada para {Phone} (tenant {TenantId}): configuracao ausente ou desativada no momento do envio.",
                PiiMasking.MaskPhone(toPhoneE164), tenantId);
            return;
        }

        var credentials = new WhatsAppCredentials(settings.PhoneNumberId, settings.AccessToken);
        var personalizedMessage = $"Ola, {customerName}!\n\n{message}";

        try
        {
            await whatsAppSender.SendAsync(credentials, toPhoneE164, personalizedMessage, cancellationToken);
            logger.LogInformation("Campanha WhatsApp enviada para {Phone} (tenant {TenantId}).", PiiMasking.MaskPhone(toPhoneE164), tenantId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao enviar campanha WhatsApp para {Phone} (tenant {TenantId}).", PiiMasking.MaskPhone(toPhoneE164), tenantId);
        }
    }
}
