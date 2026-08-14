using System.Globalization;
using Agendio.Infrastructure.Notifications;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Agendio.Modules.Scheduling.Infrastructure.Notifications;

/// <summary>
/// Confirmacao, lembretes T-24h/T-2h (Sprint 5, por e-mail), e a partir da
/// Fase 6 tambem cancelamento/reagendamento/confirmacao de presenca/pos-atendimento,
/// cada um por e-mail (sempre) e WhatsApp (so se o tenant tiver conectado a
/// integracao, ver Tenant.UpdateWhatsAppSettings). Cada metodo publico e um
/// job do Hangfire — a durabilidade (sobrevive a reinicio do processo, storage
/// no Postgres) e o retry automatico em falha transitoria (rede, SMTP/WhatsApp
/// fora do ar) sao do PROPRIO Hangfire, sem logica extra aqui.
///
/// Reagendar/cancelar um agendamento NAO cancela o job de lembrete pendente no
/// Hangfire — mais simples e mais robusto e cada job reconferir, na hora de
/// disparar, se ainda faz sentido enviar (ver LoadActiveAppointmentAsync). Um
/// lembrete de T-24h para um horario que foi remarcado vira um no-op silencioso;
/// o novo horario ja tem seu proprio lembrete agendado por
/// RescheduleAppointmentCommandHandler. Os gatilhos novos da Fase 6 (cancelado,
/// remarcado, confirmado, concluido) disparam UMA VEZ, na mesma janela da
/// requisicao que mudou o status — sem essa reconferencia de obsolescencia.
///
/// Roda fora de uma requisicao HTTP (sem JWT, sem tenant ambiente) — por isso
/// cada metodo recebe o tenantId explicito e ancora o ITenantContext manualmente,
/// o mesmo papel que ExplicitTenantBehavior cumpre para comandos publicos.
/// </summary>
public sealed class AppointmentNotificationJobs(
    SchedulingDbContext dbContext,
    ITenantContext tenantContext,
    ICustomerLookupService customerLookup,
    ITenantLookupService tenantLookup,
    IEmailSender emailSender,
    IWhatsAppSender whatsAppSender,
    ILogger<AppointmentNotificationJobs> logger)
{
    public async Task SendConfirmationEmailAsync(Guid tenantId, Guid appointmentId, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var appointment = await LoadActiveAppointmentAsync(appointmentId, expectedStartUtc: null, cancellationToken);
        if (appointment is null)
        {
            return;
        }

        await NotifyAsync(appointment, WhatsAppTrigger.Scheduled, "Agendamento confirmado", null, cancellationToken);
    }

    public async Task SendReminderEmailAsync(
        Guid tenantId, Guid appointmentId, DateTimeOffset expectedStartUtc, string reminderLabel, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var appointment = await LoadActiveAppointmentAsync(appointmentId, expectedStartUtc, cancellationToken);
        if (appointment is null)
        {
            return;
        }

        await NotifyAsync(appointment, WhatsAppTrigger.Reminder, "Lembrete de agendamento", reminderLabel, cancellationToken);
    }

    public async Task SendCancellationNotificationAsync(Guid tenantId, Guid appointmentId, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var appointment = await LoadAppointmentAsync(appointmentId, cancellationToken);
        if (appointment is null)
        {
            return;
        }

        await NotifyAsync(appointment, WhatsAppTrigger.Cancelled, "Agendamento cancelado", null, cancellationToken);
    }

    public async Task SendRescheduleNotificationAsync(Guid tenantId, Guid appointmentId, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var appointment = await LoadAppointmentAsync(appointmentId, cancellationToken);
        if (appointment is null)
        {
            return;
        }

        await NotifyAsync(appointment, WhatsAppTrigger.Rescheduled, "Agendamento remarcado", null, cancellationToken);
    }

    public async Task SendConfirmedAttendanceNotificationAsync(Guid tenantId, Guid appointmentId, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var appointment = await LoadAppointmentAsync(appointmentId, cancellationToken);
        if (appointment is null)
        {
            return;
        }

        await NotifyAsync(appointment, WhatsAppTrigger.Confirmed, "Presenca confirmada", null, cancellationToken);
    }

    public async Task SendCompletedNotificationAsync(Guid tenantId, Guid appointmentId, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var appointment = await LoadAppointmentAsync(appointmentId, cancellationToken);
        if (appointment is null)
        {
            return;
        }

        await NotifyAsync(appointment, WhatsAppTrigger.Completed, "Obrigado pela visita", null, cancellationToken);
    }

    private async Task<Appointment?> LoadAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken) =>
        await dbContext.Appointments.AsNoTracking().SingleOrDefaultAsync(a => a.Id == AppointmentId.From(appointmentId), cancellationToken);

    private async Task<Appointment?> LoadActiveAppointmentAsync(
        Guid appointmentId, DateTimeOffset? expectedStartUtc, CancellationToken cancellationToken)
    {
        var appointment = await LoadAppointmentAsync(appointmentId, cancellationToken);

        if (appointment is null)
        {
            return null;
        }

        if (appointment.Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Confirmed))
        {
            return null;
        }

        // Comparacao com tolerancia, nunca igualdade exata: o Postgres guarda
        // timestamptz com precisao de microssegundos, .NET DateTimeOffset tem
        // ticks (100ns) — o mesmo instante pode arredondar de forma diferente
        // no round-trip. Igualdade exata aqui faria TODO lembrete legitimo
        // (nao remarcado) ser tratado como obsoleto e nunca enviado.
        if (expectedStartUtc is { } expected && (appointment.Slot.StartUtc - expected).Duration() > TimeSpan.FromSeconds(1))
        {
            return null;
        }

        return appointment;
    }

    private async Task NotifyAsync(
        Appointment appointment, WhatsAppTrigger trigger, string emailSubjectPrefix, string? reminderLabel, CancellationToken cancellationToken)
    {
        var customer = await customerLookup.FindByIdAsync(appointment.CustomerId, cancellationToken);
        if (customer is null)
        {
            logger.LogWarning("Agendamento {AppointmentId} sem cliente cadastrado — notificacao nao enviada.", appointment.Id.Value);
            return;
        }

        var tenant = await tenantLookup.FindByIdAsync(appointment.TenantId, cancellationToken);
        var tenantName = tenant?.Name ?? "seu estabelecimento";

        var localStart = appointment.Slot.StartUtc;
        if (tenant is not null)
        {
            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZoneId);
                localStart = TimeZoneInfo.ConvertTime(appointment.Slot.StartUtc, timeZone);
            }
            catch (TimeZoneNotFoundException)
            {
                // Mantem UTC como fallback — melhor mostrar um horario levemente
                // errado do que falhar o envio inteiro da notificacao.
            }
        }

        if (customer.Email is not null)
        {
            var headline = reminderLabel is null ? $"{emailSubjectPrefix}!" : $"Lembrete: seu agendamento e {reminderLabel}!";
            var html = $"""
                <p>{headline}</p>
                <p><strong>{appointment.ServiceName}</strong></p>
                <p>{localStart:dddd, dd/MM/yyyy 'as' HH:mm}</p>
                <p>{tenantName}</p>
                """;

            await emailSender.SendAsync(customer.Email, $"{emailSubjectPrefix} — {tenantName}", html, cancellationToken);
        }

        await TrySendWhatsAppAsync(appointment.TenantId, trigger, customer.Phone, customer.FullName, appointment.ServiceName, localStart, tenantName, cancellationToken);
    }

    private async Task TrySendWhatsAppAsync(
        TenantId tenantId,
        WhatsAppTrigger trigger,
        string? customerPhone,
        string customerName,
        string serviceName,
        DateTimeOffset localStart,
        string tenantName,
        CancellationToken cancellationToken)
    {
        if (customerPhone is null)
        {
            return;
        }

        var settings = await tenantLookup.GetWhatsAppSettingsAsync(tenantId, cancellationToken);
        if (settings is null || !settings.Enabled || settings.PhoneNumberId is null || settings.AccessToken is null)
        {
            return;
        }

        var template = SelectTemplate(settings, trigger) ?? WhatsAppMessageDefaults.For(trigger);
        var message = RenderTemplate(template, customerName, serviceName, localStart, tenantName);
        var credentials = new WhatsAppCredentials(settings.PhoneNumberId, settings.AccessToken);

        await whatsAppSender.SendAsync(credentials, customerPhone, message, cancellationToken);
    }

    private static string? SelectTemplate(TenantWhatsAppSettings settings, WhatsAppTrigger trigger) => trigger switch
    {
        WhatsAppTrigger.Scheduled => settings.ScheduledTemplate,
        WhatsAppTrigger.Reminder => settings.ReminderTemplate,
        WhatsAppTrigger.Cancelled => settings.CancelledTemplate,
        WhatsAppTrigger.Rescheduled => settings.RescheduledTemplate,
        WhatsAppTrigger.Confirmed => settings.ConfirmedTemplate,
        WhatsAppTrigger.Completed => settings.CompletedTemplate,
        _ => null,
    };

    private static string RenderTemplate(string template, string customerName, string serviceName, DateTimeOffset localStart, string tenantName) =>
        template
            .Replace("{{cliente}}", customerName)
            .Replace("{{servico}}", serviceName)
            .Replace("{{data}}", localStart.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))
            .Replace("{{hora}}", localStart.ToString("HH:mm", CultureInfo.InvariantCulture))
            .Replace("{{estabelecimento}}", tenantName);
}

/// <summary>Os 6 gatilhos de notificacao ligados a mudanca de status de um agendamento — campanhas (Fase 21) e recuperacao de cliente (Fase 10) usam WhatsAppCredentials/RenderTemplate diretamente quando chegar a vez, sem precisar de um trigger aqui.</summary>
internal enum WhatsAppTrigger
{
    Scheduled,
    Reminder,
    Cancelled,
    Rescheduled,
    Confirmed,
    Completed,
}

/// <summary>Texto usado quando o tenant nao customizou o template do gatilho — placeholders: {{cliente}}, {{servico}}, {{data}}, {{hora}}, {{estabelecimento}}.</summary>
internal static class WhatsAppMessageDefaults
{
    public static string For(WhatsAppTrigger trigger) => trigger switch
    {
        WhatsAppTrigger.Scheduled =>
            "Ola {{cliente}}! Seu agendamento de {{servico}} em {{estabelecimento}} foi confirmado para {{data}} as {{hora}}.",
        WhatsAppTrigger.Reminder =>
            "Lembrete: seu agendamento de {{servico}} em {{estabelecimento}} e {{data}} as {{hora}}.",
        WhatsAppTrigger.Cancelled =>
            "Seu agendamento de {{servico}} em {{estabelecimento}} marcado para {{data}} as {{hora}} foi cancelado.",
        WhatsAppTrigger.Rescheduled =>
            "Seu agendamento de {{servico}} em {{estabelecimento}} foi remarcado para {{data}} as {{hora}}.",
        WhatsAppTrigger.Confirmed =>
            "Seu agendamento de {{servico}} em {{estabelecimento}} para {{data}} as {{hora}} esta confirmado. Contamos com voce!",
        WhatsAppTrigger.Completed =>
            "Obrigado por visitar {{estabelecimento}}, {{cliente}}! Esperamos que tenha gostado do seu {{servico}}.",
        _ => throw new ArgumentOutOfRangeException(nameof(trigger)),
    };
}
