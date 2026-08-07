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
/// Confirmacao imediata e lembretes T-24h/T-2h (Sprint 5). Cada metodo publico
/// e um job do Hangfire — a durabilidade (sobrevive a reinicio do processo,
/// storage no Postgres) e o retry automatico em falha transitoria (rede, SMTP
/// fora do ar) sao do PROPRIO Hangfire, sem logica extra aqui.
///
/// Reagendar/cancelar um agendamento NAO cancela o job pendente no Hangfire —
/// mais simples e mais robusto e cada job reconferir, na hora de disparar, se
/// ainda faz sentido enviar (ver LoadActiveAppointmentAsync). Um lembrete de
/// T-24h para um horario que foi remarcado vira um no-op silencioso; o novo
/// horario ja tem seu proprio lembrete agendado por RescheduleAppointmentCommandHandler.
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

        await SendAsync(appointment, "Agendamento confirmado", null, cancellationToken);
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

        await SendAsync(appointment, "Lembrete de agendamento", reminderLabel, cancellationToken);
    }

    private async Task<Appointment?> LoadActiveAppointmentAsync(
        Guid appointmentId, DateTimeOffset? expectedStartUtc, CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == AppointmentId.From(appointmentId), cancellationToken);

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

    private async Task SendAsync(Appointment appointment, string subjectPrefix, string? reminderLabel, CancellationToken cancellationToken)
    {
        var customer = await customerLookup.FindByIdAsync(appointment.CustomerId, cancellationToken);
        if (customer?.Email is null)
        {
            logger.LogWarning("Agendamento {AppointmentId} sem cliente com e-mail — notificacao nao enviada.", appointment.Id.Value);
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
                // errado do que falhar o envio inteiro do lembrete.
            }
        }

        var headline = reminderLabel is null
            ? "Seu agendamento foi confirmado!"
            : $"Lembrete: seu agendamento e {reminderLabel}!";

        var html = $"""
            <p>{headline}</p>
            <p><strong>{appointment.ServiceName}</strong></p>
            <p>{localStart:dddd, dd/MM/yyyy 'as' HH:mm}</p>
            <p>{tenantName}</p>
            """;

        await emailSender.SendAsync(customer.Email, $"{subjectPrefix} — {tenantName}", html, cancellationToken);
    }
}
