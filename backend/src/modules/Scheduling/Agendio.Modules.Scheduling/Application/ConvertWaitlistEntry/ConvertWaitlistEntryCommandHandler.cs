using Agendio.Modules.Catalog.Contracts;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Scheduling.Application.Shared;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Notifications;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Agendio.SharedKernel.ValueObjects;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Agendio.Modules.Scheduling.Application.ConvertWaitlistEntry;

public sealed class ConvertWaitlistEntryCommandHandler(
    SchedulingDbContext dbContext,
    ITenantContext tenantContext,
    IClock clock,
    ICustomerLookupService customerLookup,
    IResourceLookupService resourceLookup,
    IServiceLookupService serviceLookup,
    ITenantLookupService tenantLookup,
    IBackgroundJobClient jobClient) : ICommandHandler<ConvertWaitlistEntryCommand, Guid>
{
    private const string ExclusionViolationSqlState = "23P01";

    public async Task<Result<Guid>> Handle(ConvertWaitlistEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await dbContext.WaitlistEntries
            .SingleOrDefaultAsync(w => w.Id == WaitlistEntryId.From(request.WaitlistEntryId), cancellationToken);

        if (entry is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Waitlist.NotFound", "Entrada da fila de espera nao encontrada."));
        }

        if (entry.Status is not (WaitlistStatus.Waiting or WaitlistStatus.Notified))
        {
            return Result.Failure<Guid>(Error.Validation("Waitlist.InvalidTransition", "Esta entrada nao pode mais ser confirmada."));
        }

        var customer = await customerLookup.FindByIdAsync(entry.CustomerId, cancellationToken);
        if (customer is null || !customer.IsActive)
        {
            return Result.Failure<Guid>(Error.NotFound("Appointment.CustomerNotFound", "Cliente nao encontrado ou inativo."));
        }

        var resource = await resourceLookup.FindByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null || !resource.IsActive)
        {
            return Result.Failure<Guid>(Error.NotFound("Appointment.ResourceNotFound", "Recurso nao encontrado ou inativo."));
        }

        var service = await serviceLookup.FindByIdAsync(entry.ServiceId, cancellationToken);
        if (service is null || !service.IsActive)
        {
            return Result.Failure<Guid>(Error.NotFound("Appointment.ServiceNotFound", "Servico nao encontrado ou inativo."));
        }

        if (request.StartAtUtc <= clock.UtcNow)
        {
            return Result.Failure<Guid>(Error.Validation("Appointment.StartInThePast", "Nao e possivel agendar em um horario que ja passou."));
        }

        var availabilityCheck = await AppointmentAvailabilityGuard.EnsureResourceIsAvailableAsync(
            tenantLookup, resourceLookup, tenantContext.TenantId, request.ResourceId, request.StartAtUtc, cancellationToken);
        if (availabilityCheck.IsFailure)
        {
            return Result.Failure<Guid>(availabilityCheck.Error);
        }

        var slotResult = TimeSlot.Create(request.StartAtUtc, request.StartAtUtc.AddMinutes(service.DurationMinutes));
        if (slotResult.IsFailure)
        {
            return Result.Failure<Guid>(slotResult.Error);
        }

        var priceResult = Money.Create(service.Price, service.Currency);
        if (priceResult.IsFailure)
        {
            return Result.Failure<Guid>(priceResult.Error);
        }

        // Mesma tecnica de advisory lock usada em ScheduleAppointmentCommandHandler.
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({tenantContext.TenantId.Value.ToString()}), hashtext({request.ResourceId.ToString()}))",
                cancellationToken);

            var appointmentResult = Appointment.Schedule(
                tenantContext.TenantId, entry.CustomerId, request.ResourceId, resource.UnitId, entry.ServiceId,
                service.Name, slotResult.Value, priceResult.Value, entry.Notes);

            if (appointmentResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<Guid>(appointmentResult.Error);
            }

            dbContext.Appointments.Add(appointmentResult.Value);

            var trackedEntry = await dbContext.WaitlistEntries.SingleAsync(w => w.Id == entry.Id, cancellationToken);
            var convertResult = trackedEntry.Convert(appointmentResult.Value.Id);
            if (convertResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<Guid>(convertResult.Error);
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: ExclusionViolationSqlState })
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<Guid>(
                    Error.Conflict("Appointment.SlotTaken", "Esse horario acabou de ser reservado para este recurso. Escolha outro horario."));
            }

            await transaction.CommitAsync(cancellationToken);

            var newAppointmentId = appointmentResult.Value.Id.Value;
            AppointmentNotificationScheduler.EnqueueConfirmation(jobClient, tenantContext.TenantId.Value, newAppointmentId);
            AppointmentNotificationScheduler.ScheduleReminders(jobClient, clock, tenantContext.TenantId.Value, newAppointmentId, slotResult.Value.StartUtc);

            return Result.Success(newAppointmentId);
        });
    }
}
