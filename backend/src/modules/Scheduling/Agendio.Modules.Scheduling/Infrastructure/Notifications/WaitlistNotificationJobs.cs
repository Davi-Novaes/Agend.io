using System.Globalization;
using Agendio.Infrastructure;
using Agendio.Infrastructure.Notifications;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agendio.Modules.Scheduling.Infrastructure.Notifications;

/// <summary>
/// Fase 13 — avisa uma entrada da fila de espera que uma vaga compativel abriu
/// (ver CancelAppointmentCommandHandler). Mais simples que AppointmentNotificationJobs:
/// nao ha NotificationLogEntry (essa tabela e especifica de Appointment) nem
/// gatilho configuravel — e um unico evento, sem lembrete nem historico.
/// </summary>
public sealed class WaitlistNotificationJobs(
    SchedulingDbContext dbContext,
    ITenantContext tenantContext,
    ICustomerLookupService customerLookup,
    ITenantLookupService tenantLookup,
    IEmailSender emailSender,
    IWhatsAppSender whatsAppSender,
    IOptions<FrontendOptions> frontendOptions,
    ILogger<WaitlistNotificationJobs> logger)
{
    public async Task SendSlotAvailableNotificationAsync(Guid tenantId, Guid waitlistEntryId, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var entry = await dbContext.WaitlistEntries.AsNoTracking()
            .SingleOrDefaultAsync(w => w.Id == WaitlistEntryId.From(waitlistEntryId), cancellationToken);

        if (entry is null || entry.Status != WaitlistStatus.Notified)
        {
            return;
        }

        var customer = await customerLookup.FindByIdAsync(entry.CustomerId, cancellationToken);
        if (customer is null)
        {
            logger.LogWarning("Entrada {WaitlistEntryId} da fila de espera sem cliente cadastrado — notificacao nao enviada.", waitlistEntryId);
            return;
        }

        var tenant = await tenantLookup.FindByIdAsync(entry.TenantId, cancellationToken);
        var tenantName = tenant?.Name ?? "seu estabelecimento";
        var bookingUrl = tenant is not null ? $"{frontendOptions.Value.BaseUrl}/{tenant.Slug}" : null;
        var dateLabel = entry.PreferredDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        if (customer.Email is not null)
        {
            var bookingHtml = bookingUrl is null ? "" : $"""<p><a href="{bookingUrl}">Agende agora</a></p>""";
            var html = $"""
                <p>Uma vaga abriu para <strong>{entry.ServiceName}</strong> em {dateLabel}!</p>
                <p>Entre em contato o quanto antes para confirmar — a vaga e por ordem de chegada.</p>
                <p>{tenantName}</p>
                {bookingHtml}
                """;

            try
            {
                await emailSender.SendAsync(customer.Email, $"Uma vaga abriu — {tenantName}", html, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao enviar e-mail de vaga aberta para a entrada {WaitlistEntryId} da fila de espera.", waitlistEntryId);
            }
        }

        if (customer.Phone is not null)
        {
            await TrySendWhatsAppAsync(customer.Phone, entry.ServiceName, dateLabel, tenantName, waitlistEntryId, cancellationToken);
        }
    }

    private async Task TrySendWhatsAppAsync(
        string customerPhone, string serviceName, string dateLabel, string tenantName, Guid waitlistEntryId, CancellationToken cancellationToken)
    {
        var settings = await tenantLookup.GetWhatsAppSettingsAsync(tenantContext.TenantId, cancellationToken);
        if (settings is null || !settings.Enabled || settings.PhoneNumberId is null || settings.AccessToken is null)
        {
            return;
        }

        var message =
            $"Uma vaga abriu para {serviceName} em {tenantName} no dia {dateLabel}! Entre em contato o quanto antes para confirmar — e por ordem de chegada.";
        var credentials = new WhatsAppCredentials(settings.PhoneNumberId, settings.AccessToken);

        try
        {
            await whatsAppSender.SendAsync(credentials, customerPhone, message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao enviar WhatsApp de vaga aberta para a entrada {WaitlistEntryId} da fila de espera.", waitlistEntryId);
        }
    }
}
