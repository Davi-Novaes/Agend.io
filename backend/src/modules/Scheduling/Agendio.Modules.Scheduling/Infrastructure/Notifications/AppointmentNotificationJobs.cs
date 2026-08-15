using System.Globalization;
using Agendio.Infrastructure;
using Agendio.Infrastructure.Notifications;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agendio.Modules.Scheduling.Infrastructure.Notifications;

/// <summary>
/// Confirmacao, lembretes T-24h/T-2h (Sprint 5, por e-mail), e a partir da
/// Fase 6 tambem cancelamento/reagendamento/confirmacao de presenca/pos-atendimento,
/// cada um por e-mail (sempre) e WhatsApp (so se o tenant tiver conectado a
/// integracao, ver Tenant.UpdateWhatsAppSettings). Lembrete e pos-atendimento
/// tambem respeitam os toggles da Fase 7 (Tenant.UpdateReminderSettings) — os
/// outros 4 gatilhos nao sao configuraveis, o roadmap so pede isso pros dois.
/// Cada metodo publico e um job do Hangfire — a durabilidade (sobrevive a
/// reinicio do processo, storage no Postgres) e o retry automatico em falha
/// transitoria (rede, SMTP/WhatsApp fora do ar) sao do PROPRIO Hangfire, sem
/// logica extra aqui.
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
/// Toda tentativa de envio (sucesso ou falha) vira um NotificationLogEntry —
/// "historico de mensagens" da Fase 7. Uma falha seguida de retry bem-sucedido
/// do Hangfire aparece como duas linhas de proposito: mostra que houve uma
/// falha real, sem esconder nada do dono.
///
/// Roda fora de uma requisicao HTTP (sem JWT, sem tenant ambiente) — por isso
/// cada metodo recebe o tenantId explicito e ancora o ITenantContext manualmente,
/// o mesmo papel que ExplicitTenantBehavior cumpre para comandos publicos.
/// </summary>
public sealed class AppointmentNotificationJobs(
    SchedulingDbContext dbContext,
    ITenantContext tenantContext,
    IClock clock,
    ICustomerLookupService customerLookup,
    ITenantLookupService tenantLookup,
    IEmailSender emailSender,
    IWhatsAppSender whatsAppSender,
    IOptions<FrontendOptions> frontendOptions,
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

        await NotifyAsync(appointment, NotificationTrigger.Scheduled, "Agendamento confirmado", null, cancellationToken);
    }

    public async Task SendReminderEmailAsync(
        Guid tenantId, Guid appointmentId, DateTimeOffset expectedStartUtc, ReminderLeadTime leadTime, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var notificationSettings = await tenantLookup.GetNotificationSettingsAsync(TenantId.From(tenantId), cancellationToken);
        var reminderEnabled = leadTime == ReminderLeadTime.TwentyFourHours
            ? notificationSettings?.Reminder24hEnabled ?? true
            : notificationSettings?.Reminder2hEnabled ?? true;
        if (!reminderEnabled)
        {
            return;
        }

        var appointment = await LoadActiveAppointmentAsync(appointmentId, expectedStartUtc, cancellationToken);
        if (appointment is null)
        {
            return;
        }

        var reminderLabel = leadTime == ReminderLeadTime.TwentyFourHours ? "em 24 horas" : "em 2 horas";
        await NotifyAsync(appointment, NotificationTrigger.Reminder, "Lembrete de agendamento", reminderLabel, cancellationToken);
    }

    public async Task SendCancellationNotificationAsync(Guid tenantId, Guid appointmentId, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var appointment = await LoadAppointmentAsync(appointmentId, cancellationToken);
        if (appointment is null)
        {
            return;
        }

        await NotifyAsync(appointment, NotificationTrigger.Cancelled, "Agendamento cancelado", null, cancellationToken);
    }

    public async Task SendRescheduleNotificationAsync(Guid tenantId, Guid appointmentId, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var appointment = await LoadAppointmentAsync(appointmentId, cancellationToken);
        if (appointment is null)
        {
            return;
        }

        await NotifyAsync(appointment, NotificationTrigger.Rescheduled, "Agendamento remarcado", null, cancellationToken);
    }

    public async Task SendConfirmedAttendanceNotificationAsync(Guid tenantId, Guid appointmentId, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var appointment = await LoadAppointmentAsync(appointmentId, cancellationToken);
        if (appointment is null)
        {
            return;
        }

        await NotifyAsync(appointment, NotificationTrigger.Confirmed, "Presenca confirmada", null, cancellationToken);
    }

    public async Task SendCompletedNotificationAsync(Guid tenantId, Guid appointmentId, CancellationToken cancellationToken)
    {
        tenantContext.SetTenant(TenantId.From(tenantId));

        var notificationSettings = await tenantLookup.GetNotificationSettingsAsync(TenantId.From(tenantId), cancellationToken);
        if (notificationSettings is not null && !notificationSettings.PostServiceThankYouEnabled)
        {
            return;
        }

        var appointment = await LoadAppointmentAsync(appointmentId, cancellationToken);
        if (appointment is null)
        {
            return;
        }

        await NotifyAsync(appointment, NotificationTrigger.Completed, "Obrigado pela visita", null, cancellationToken);
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
        Appointment appointment, NotificationTrigger trigger, string emailSubjectPrefix, string? reminderLabel, CancellationToken cancellationToken)
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

        // So o pos-atendimento leva link de avaliacao — os outros 5 gatilhos nao fazem sentido pedir review ainda.
        var reviewUrl = trigger == NotificationTrigger.Completed && tenant is not null
            ? $"{frontendOptions.Value.BaseUrl}/{tenant.Slug}/avaliar?appointmentId={appointment.Id.Value}"
            : null;

        if (customer.Email is not null)
        {
            var headline = reminderLabel is null ? $"{emailSubjectPrefix}!" : $"Lembrete: seu agendamento e {reminderLabel}!";
            var reviewHtml = reviewUrl is null ? "" : $"""<p><a href="{reviewUrl}">Avalie seu atendimento</a></p>""";
            var html = $"""
                <p>{headline}</p>
                <p><strong>{appointment.ServiceName}</strong></p>
                <p>{localStart:dddd, dd/MM/yyyy 'as' HH:mm}</p>
                <p>{tenantName}</p>
                {reviewHtml}
                """;

            try
            {
                await emailSender.SendAsync(customer.Email, $"{emailSubjectPrefix} — {tenantName}", html, cancellationToken);
                await RecordNotificationAsync(appointment, customer.CustomerId, NotificationChannel.Email, trigger, succeeded: true, errorMessage: null, cancellationToken);
            }
            catch (Exception ex)
            {
                await RecordNotificationAsync(appointment, customer.CustomerId, NotificationChannel.Email, trigger, succeeded: false, ex.Message, cancellationToken);
                throw;
            }
        }

        await TrySendWhatsAppAsync(
            appointment, customer.CustomerId, trigger, customer.Phone, customer.FullName, appointment.ServiceName, localStart, tenantName, reviewUrl, cancellationToken);
    }

    private async Task TrySendWhatsAppAsync(
        Appointment appointment,
        Guid customerId,
        NotificationTrigger trigger,
        string? customerPhone,
        string customerName,
        string serviceName,
        DateTimeOffset localStart,
        string tenantName,
        string? reviewUrl,
        CancellationToken cancellationToken)
    {
        if (customerPhone is null)
        {
            return;
        }

        var settings = await tenantLookup.GetWhatsAppSettingsAsync(appointment.TenantId, cancellationToken);
        if (settings is null || !settings.Enabled || settings.PhoneNumberId is null || settings.AccessToken is null)
        {
            return;
        }

        var template = SelectTemplate(settings, trigger) ?? WhatsAppMessageDefaults.For(trigger);
        var message = RenderTemplate(template, customerName, serviceName, localStart, tenantName, reviewUrl);
        var credentials = new WhatsAppCredentials(settings.PhoneNumberId, settings.AccessToken);

        try
        {
            await whatsAppSender.SendAsync(credentials, customerPhone, message, cancellationToken);
            await RecordNotificationAsync(appointment, customerId, NotificationChannel.WhatsApp, trigger, succeeded: true, errorMessage: null, cancellationToken);
        }
        catch (Exception ex)
        {
            await RecordNotificationAsync(appointment, customerId, NotificationChannel.WhatsApp, trigger, succeeded: false, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task RecordNotificationAsync(
        Appointment appointment, Guid customerId, NotificationChannel channel, NotificationTrigger trigger, bool succeeded, string? errorMessage,
        CancellationToken cancellationToken)
    {
        var entryResult = succeeded
            ? NotificationLogEntry.RecordSent(appointment.TenantId, appointment.Id, customerId, channel, trigger, clock.UtcNow)
            : NotificationLogEntry.RecordFailed(
                appointment.TenantId, appointment.Id, customerId, channel, trigger, clock.UtcNow, errorMessage ?? "Erro desconhecido.");

        if (entryResult.IsFailure)
        {
            logger.LogWarning("Nao foi possivel registrar o historico de notificacao: {Error}", entryResult.Error.Message);
            return;
        }

        dbContext.NotificationLogEntries.Add(entryResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? SelectTemplate(TenantWhatsAppSettings settings, NotificationTrigger trigger) => trigger switch
    {
        NotificationTrigger.Scheduled => settings.ScheduledTemplate,
        NotificationTrigger.Reminder => settings.ReminderTemplate,
        NotificationTrigger.Cancelled => settings.CancelledTemplate,
        NotificationTrigger.Rescheduled => settings.RescheduledTemplate,
        NotificationTrigger.Confirmed => settings.ConfirmedTemplate,
        NotificationTrigger.Completed => settings.CompletedTemplate,
        _ => null,
    };

    private static string RenderTemplate(
        string template, string customerName, string serviceName, DateTimeOffset localStart, string tenantName, string? reviewUrl) =>
        template
            .Replace("{{cliente}}", customerName)
            .Replace("{{servico}}", serviceName)
            .Replace("{{data}}", localStart.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))
            .Replace("{{hora}}", localStart.ToString("HH:mm", CultureInfo.InvariantCulture))
            .Replace("{{estabelecimento}}", tenantName)
            .Replace("{{link_avaliacao}}", reviewUrl ?? "");
}

/// <summary>Qual dos dois lembretes automaticos — cada um com seu proprio toggle (Tenant.Reminder24hEnabled/Reminder2hEnabled, Fase 7).</summary>
public enum ReminderLeadTime
{
    TwentyFourHours,
    TwoHours,
}

/// <summary>
/// Texto usado quando o tenant nao customizou o template do gatilho — placeholders:
/// {{cliente}}, {{servico}}, {{data}}, {{hora}}, {{estabelecimento}}, {{link_avaliacao}}
/// (Fase 12 — so preenchido no gatilho Completed, vazio nos demais).
/// </summary>
internal static class WhatsAppMessageDefaults
{
    public static string For(NotificationTrigger trigger) => trigger switch
    {
        NotificationTrigger.Scheduled =>
            "Ola {{cliente}}! Seu agendamento de {{servico}} em {{estabelecimento}} foi confirmado para {{data}} as {{hora}}.",
        NotificationTrigger.Reminder =>
            "Lembrete: seu agendamento de {{servico}} em {{estabelecimento}} e {{data}} as {{hora}}.",
        NotificationTrigger.Cancelled =>
            "Seu agendamento de {{servico}} em {{estabelecimento}} marcado para {{data}} as {{hora}} foi cancelado.",
        NotificationTrigger.Rescheduled =>
            "Seu agendamento de {{servico}} em {{estabelecimento}} foi remarcado para {{data}} as {{hora}}.",
        NotificationTrigger.Confirmed =>
            "Seu agendamento de {{servico}} em {{estabelecimento}} para {{data}} as {{hora}} esta confirmado. Contamos com voce!",
        NotificationTrigger.Completed =>
            "Obrigado por visitar {{estabelecimento}}, {{cliente}}! Esperamos que tenha gostado do seu {{servico}}. Avalie seu atendimento: {{link_avaliacao}}",
        _ => throw new ArgumentOutOfRangeException(nameof(trigger)),
    };
}
