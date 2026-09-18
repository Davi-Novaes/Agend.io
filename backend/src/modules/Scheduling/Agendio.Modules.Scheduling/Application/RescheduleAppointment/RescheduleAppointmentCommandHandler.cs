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

        // Reatribuir recurso (drag entre colunas na Agenda) usa o mesmo campo
        // do que so remarcar horario/duracao — targetResourceId cai no recurso
        // atual quando NewResourceId nao vem preenchido.
        var targetResourceId = request.NewResourceId ?? appointment.ResourceId;

        // Resolvido sempre (mesmo quando o recurso nao mudou) para cobrir de
        // graca um caso que o reschedule nunca validou: o recurso ficou
        // inativo/foi excluido depois que o agendamento original foi criado.
        var targetResource = await resourceLookup.FindByIdAsync(targetResourceId, cancellationToken);
        if (targetResource is null || !targetResource.IsActive)
        {
            return Result.Failure(Error.NotFound("Appointment.ResourceNotFound", "Recurso nao encontrado ou inativo."));
        }

        var availabilityCheck = await AppointmentAvailabilityGuard.EnsureResourceIsAvailableAsync(
            tenantLookup, resourceLookup, appointment.TenantId, targetResourceId, request.NewStartAtUtc, cancellationToken);
        if (availabilityCheck.IsFailure)
        {
            return availabilityCheck;
        }

        // Preserva a duracao original por padrao — remarcar muda O QUANDO, nao
        // o quanto o servico dura — a menos que NewDurationMinutes venha
        // preenchido (redimensionar arrastando a borda do card na Agenda).
        var duration = request.NewDurationMinutes is { } newDurationMinutes
            ? TimeSpan.FromMinutes(newDurationMinutes)
            : appointment.Slot.Duration;
        var newSlotResult = TimeSlot.Create(request.NewStartAtUtc, request.NewStartAtUtc.Add(duration));
        if (newSlotResult.IsFailure)
        {
            return Result.Failure(newSlotResult.Error);
        }

        // Nao ha checagem de horario de funcionamento aqui de proposito: a
        // Agenda trabalha com grade de 24h e permite marcar em qualquer
        // horario (decisao de produto), e ScheduleAppointment/PublicSchedule
        // tambem nunca validaram expediente — validar so no reschedule criaria
        // a inconsistencia de "da pra criar as 22h, mas nao da pra mover pras
        // 22h". Data fechada e folga do recurso continuam bloqueando, via
        // AppointmentAvailabilityGuard acima.

        // Mesma tecnica de advisory lock usada em ScheduleAppointmentCommandHandler
        // — evita deadlock (40P01) entre transacoes concorrentes disputando o
        // indice GIST da EXCLUDE constraint no mesmo recurso.
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Trava no recurso de DESTINO (pode ser o mesmo de sempre, ou o
            // novo quando e uma reatribuicao) — e contra ele que a EXCLUDE
            // constraint vai correr.
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({appointment.TenantId.Value.ToString()}), hashtext({targetResourceId.ToString()}))",
                cancellationToken);

            var trackedAppointment = await dbContext.Appointments
                .SingleOrDefaultAsync(a => a.Id == AppointmentId.From(request.AppointmentId), cancellationToken);

            if (trackedAppointment is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(Error.NotFound("Appointment.NotFound", "Agendamento nao encontrado."));
            }

            var previousStartUtc = trackedAppointment.Slot.StartUtc;

            var rescheduleResult = trackedAppointment.Reschedule(newSlotResult.Value, request.NewResourceId, request.NewResourceId is not null ? targetResource.UnitId : null);
            if (rescheduleResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return rescheduleResult;
            }

            // trackedAppointment.ResourceId ja reflete o recurso NOVO aqui (Reschedule ja rodou acima) — PreviousResourceId so vem preenchido quando de fato mudou.
            var logEntryResult = AppointmentChangeLogEntry.RecordReschedule(
                trackedAppointment.TenantId, trackedAppointment.Id, trackedAppointment.CustomerId, trackedAppointment.ResourceId,
                trackedAppointment.ServiceName, previousStartUtc, newSlotResult.Value.StartUtc, request.Reason, clock.UtcNow,
                previousResourceId: request.NewResourceId is not null && request.NewResourceId != appointment.ResourceId ? appointment.ResourceId : null,
                newEndUtc: request.NewDurationMinutes is not null ? newSlotResult.Value.EndUtc : null);
            if (logEntryResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(logEntryResult.Error);
            }

            dbContext.AppointmentChangeLogEntries.Add(logEntryResult.Value);

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
            AppointmentNotificationScheduler.EnqueueReschedule(jobClient, trackedAppointment.TenantId.Value, trackedAppointment.Id.Value);

            return Result.Success();
        });
    }
}
