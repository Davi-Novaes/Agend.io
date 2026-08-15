using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Notifications;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.CancelAppointment;

public sealed class CancelAppointmentCommandHandler(
    SchedulingDbContext dbContext, IClock clock, ITenantLookupService tenantLookup, IBackgroundJobClient jobClient)
    : ICommandHandler<CancelAppointmentCommand>
{
    public async Task<Result> Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments
            .SingleOrDefaultAsync(a => a.Id == AppointmentId.From(request.AppointmentId), cancellationToken);

        if (appointment is null)
        {
            return Result.Failure(Error.NotFound("Appointment.NotFound", "Agendamento nao encontrado."));
        }

        var result = request.ByStaff ? appointment.CancelByStaff() : appointment.CancelByCustomer();
        if (result.IsFailure)
        {
            return result;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        AppointmentNotificationScheduler.EnqueueCancellation(jobClient, appointment.TenantId.Value, appointment.Id.Value);

        await NotifyMatchingWaitlistEntriesAsync(appointment, cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Fase 13 — a vaga que acabou de abrir pode interessar a mais de uma
    /// entrada Aguardando (mesmo servico/data, sem preferencia de profissional
    /// ou preferindo justamente este). Todas sao notificadas; a equipe confirma
    /// manualmente qual delas vira agendamento (ver ConvertWaitlistEntryCommandHandler)
    /// — nao ha reserva automatica por ordem de chegada.
    /// </summary>
    private async Task NotifyMatchingWaitlistEntriesAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        var tenant = await tenantLookup.FindByIdAsync(appointment.TenantId, cancellationToken);
        if (tenant is null)
        {
            return;
        }

        DateOnly preferredDate;
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZoneId);
            preferredDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(appointment.Slot.StartUtc, timeZone).DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            preferredDate = DateOnly.FromDateTime(appointment.Slot.StartUtc.UtcDateTime);
        }

        var candidates = await dbContext.WaitlistEntries
            .Where(w => w.Status == WaitlistStatus.Waiting
                && w.ServiceId == appointment.ServiceId
                && w.PreferredDate == preferredDate
                && (w.ResourceId == null || w.ResourceId == appointment.ResourceId))
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            candidate.MarkNotified(clock.UtcNow);
        }

        if (candidates.Count == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            jobClient.Enqueue<WaitlistNotificationJobs>(
                jobs => jobs.SendSlotAvailableNotificationAsync(appointment.TenantId.Value, candidate.Id.Value, CancellationToken.None));
        }
    }
}
