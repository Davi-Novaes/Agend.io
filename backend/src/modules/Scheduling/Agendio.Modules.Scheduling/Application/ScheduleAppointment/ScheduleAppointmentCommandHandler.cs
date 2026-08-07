using Agendio.Modules.Catalog.Contracts;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Notifications;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Agendio.SharedKernel.ValueObjects;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Agendio.Modules.Scheduling.Application.ScheduleAppointment;

public sealed class ScheduleAppointmentCommandHandler(
    SchedulingDbContext dbContext,
    ITenantContext tenantContext,
    IClock clock,
    ICustomerLookupService customerLookup,
    IResourceLookupService resourceLookup,
    IServiceLookupService serviceLookup,
    IBackgroundJobClient jobClient) : ICommandHandler<ScheduleAppointmentCommand, Guid>
{
    // Codigo de erro do Postgres para violacao de EXCLUDE constraint — e assim
    // que a EXCLUDE constraint em (tenant_id, resource_id, time_range) (ver
    // migration) se manifesta pro .NET quando duas requisicoes concorrentes
    // tentam reservar o mesmo recurso no mesmo horario.
    private const string ExclusionViolationSqlState = "23P01";

    public async Task<Result<Guid>> Handle(ScheduleAppointmentCommand request, CancellationToken cancellationToken)
    {
        var customer = await customerLookup.FindByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null || !customer.IsActive)
        {
            return Result.Failure<Guid>(Error.NotFound("Appointment.CustomerNotFound", "Cliente nao encontrado ou inativo."));
        }

        var resource = await resourceLookup.FindByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null || !resource.IsActive)
        {
            return Result.Failure<Guid>(Error.NotFound("Appointment.ResourceNotFound", "Recurso nao encontrado ou inativo."));
        }

        var service = await serviceLookup.FindByIdAsync(request.ServiceId, cancellationToken);
        if (service is null || !service.IsActive)
        {
            return Result.Failure<Guid>(Error.NotFound("Appointment.ServiceNotFound", "Servico nao encontrado ou inativo."));
        }

        if (request.StartAtUtc <= clock.UtcNow)
        {
            return Result.Failure<Guid>(Error.Validation("Appointment.StartInThePast", "Nao e possivel agendar em um horario que ja passou."));
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

        // A EXCLUDE constraint (ver migration) garante que nunca existam duas
        // reservas sobrepostas no mesmo recurso — mas sob MUITA concorrencia
        // real, varias transacoes inserindo ranges sobrepostos no mesmo indice
        // GIST podem se deadlockar entre si (Postgres 40P01) em vez de uma
        // simplesmente esperar a outra. O advisory lock abaixo serializa as
        // tentativas para o MESMO (tenant, recurso) antes de cada uma checar a
        // constraint, eliminando o deadlock sem enfraquecer a garantia — quem
        // decide "sem overbooking" continua sendo o Postgres, nao este lock.
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({tenantContext.TenantId.Value.ToString()}), hashtext({request.ResourceId.ToString()}))",
                cancellationToken);

            var appointmentResult = Appointment.Schedule(
                tenantContext.TenantId, request.CustomerId, request.ResourceId, request.ServiceId,
                service.Name, slotResult.Value, priceResult.Value, request.Notes);

            if (appointmentResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<Guid>(appointmentResult.Error);
            }

            dbContext.Appointments.Add(appointmentResult.Value);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsSlotAlreadyTaken(ex))
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

    private static bool IsSlotAlreadyTaken(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: ExclusionViolationSqlState };
}
