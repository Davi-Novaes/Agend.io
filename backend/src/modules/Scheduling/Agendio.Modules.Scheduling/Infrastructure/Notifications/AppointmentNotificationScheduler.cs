using Agendio.SharedKernel.Time;
using Hangfire;

namespace Agendio.Modules.Scheduling.Infrastructure.Notifications;

/// <summary>
/// Ponto unico onde Schedule/PublicSchedule/Reschedule enfileiram as
/// notificacoes — evita repetir a conta de "T-24h/T-2h so faz sentido se
/// ainda estiver no futuro" em tres handlers diferentes.
/// </summary>
public static class AppointmentNotificationScheduler
{
    private static readonly TimeSpan ReminderBefore24h = TimeSpan.FromHours(24);
    private static readonly TimeSpan ReminderBefore2h = TimeSpan.FromHours(2);

    public static void EnqueueConfirmation(IBackgroundJobClient jobClient, Guid tenantId, Guid appointmentId)
    {
        jobClient.Enqueue<AppointmentNotificationJobs>(
            job => job.SendConfirmationEmailAsync(tenantId, appointmentId, CancellationToken.None));
    }

    public static void EnqueueCancellation(IBackgroundJobClient jobClient, Guid tenantId, Guid appointmentId)
    {
        jobClient.Enqueue<AppointmentNotificationJobs>(
            job => job.SendCancellationNotificationAsync(tenantId, appointmentId, CancellationToken.None));
    }

    public static void EnqueueReschedule(IBackgroundJobClient jobClient, Guid tenantId, Guid appointmentId)
    {
        jobClient.Enqueue<AppointmentNotificationJobs>(
            job => job.SendRescheduleNotificationAsync(tenantId, appointmentId, CancellationToken.None));
    }

    public static void EnqueueConfirmedAttendance(IBackgroundJobClient jobClient, Guid tenantId, Guid appointmentId)
    {
        jobClient.Enqueue<AppointmentNotificationJobs>(
            job => job.SendConfirmedAttendanceNotificationAsync(tenantId, appointmentId, CancellationToken.None));
    }

    public static void EnqueueCompleted(IBackgroundJobClient jobClient, Guid tenantId, Guid appointmentId)
    {
        jobClient.Enqueue<AppointmentNotificationJobs>(
            job => job.SendCompletedNotificationAsync(tenantId, appointmentId, CancellationToken.None));
    }

    public static void ScheduleReminders(IBackgroundJobClient jobClient, IClock clock, Guid tenantId, Guid appointmentId, DateTimeOffset startUtc)
    {
        ScheduleReminderIfInFuture(jobClient, clock, tenantId, appointmentId, startUtc, ReminderBefore24h, ReminderLeadTime.TwentyFourHours);
        ScheduleReminderIfInFuture(jobClient, clock, tenantId, appointmentId, startUtc, ReminderBefore2h, ReminderLeadTime.TwoHours);
    }

    private static void ScheduleReminderIfInFuture(
        IBackgroundJobClient jobClient, IClock clock, Guid tenantId, Guid appointmentId, DateTimeOffset startUtc, TimeSpan leadTime, ReminderLeadTime reminderLeadTime)
    {
        var fireAtUtc = startUtc - leadTime;
        var delay = fireAtUtc - clock.UtcNow;

        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        jobClient.Schedule<AppointmentNotificationJobs>(
            job => job.SendReminderEmailAsync(tenantId, appointmentId, startUtc, reminderLeadTime, CancellationToken.None), delay);
    }
}
