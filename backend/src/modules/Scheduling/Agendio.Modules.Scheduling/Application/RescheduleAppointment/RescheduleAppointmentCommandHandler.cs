using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Scheduling.Application.Shared;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Notifications;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Agendio.SharedKernel.ValueObjects;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Agendio.Modules.Scheduling.Application.RescheduleAppointment;

public sealed class RescheduleAppointmentCommandHandler(
    SchedulingDbContext dbContext,
    IClock clock,
    IResourceLookupService resourceLookup,
    ITenantLookupService tenantLookup,
    IBackgroundJobClient jobClient) : ICommandHandler<RescheduleAppointmentCommand>
{
    private const string ExclusionViolationSqlState = "23P01";

    public async Task<Result> Handle(RescheduleAppointmentCommand request, CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == AppointmentId.From(request.AppointmentId), cancellationToken);

        if (appointment is null)
        {
            return Result.Failure(Error.NotFound("Appointment.NotFound", "Agendamento nao encontrado."));
        }

        if (request.NewStartAtUtc <= clock.UtcNow)
        {
            return Result.Failure(Error.Validation("Appointment.StartInThePast", "Nao e possivel remarcar para um horario que ja passou."));
        }

        var availabilityCheck = await AppointmentAvailabilityGuard.EnsureResourceIsAvailableAsync(
            tenantLookup, resourceLookup, appointment.TenantId, appointment.ResourceId, request.NewStartAtUtc, cancellationToken);
        if (availabilityCheck.IsFailure)
        {
            return availabilityCheck;
        }

        // Preserva a duracao original — remarcar muda O QUANDO, nao o quanto o
        // servico dura.
        var duration = appointment.Slot.Duration;
        var newSlotResult = TimeSlot.Create(request.NewStartAtUtc, request.NewStartAtUtc.Add(duration));
        if (newSlotResult.IsFailure)
        {
            return Result.Failure(newSlotResult.Error);
        }

        // Mesma tecnica de advisory lock usada em ScheduleAppointmentCommandHandler
        // — evita deadlock (40P01) entre transacoes concorrentes disputando o
        // indice GIST da EXCLUDE constraint no mesmo recurso.
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({appointment.TenantId.Value.ToString()}), hashtext({appointment.ResourceId.ToString()}))",
                cancellationToken);

            var trackedAppointment = await dbContext.Appointments
                .SingleOrDefaultAsync(a => a.Id == AppointmentId.From(request.AppointmentId), cancellationToken);

            if (trackedAppointment is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(Error.NotFound("Appointment.NotFound", "Agendamento nao encontrado."));
            }

            var rescheduleResult = trackedAppointment.Reschedule(newSlotResult.Value);
            if (rescheduleResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return rescheduleResult;
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: ExclusionViolationSqlState })
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(
                    Error.Conflict("Appointment.SlotTaken", "Esse horario acabou de ser reservado para este recurso. Escolha outro horario."));
            }

            await transaction.CommitAsync(cancellationToken);

            // Nao cancela os lembretes antigos: eles viram no-op sozinhos ao
            // disparar, porque o horario esperado deles nao bate mais com o
            // agendamento (ver AppointmentNotificationJobs).
            AppointmentNotificationScheduler.ScheduleReminders(
                jobClient, clock, trackedAppointment.TenantId.Value, trackedAppointment.Id.Value, newSlotResult.Value.StartUtc);

            return Result.Success();
        });
    }
}
