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

namespace Agendio.Modules.Scheduling.Application.PublicScheduleAppointment;

/// <summary>
/// Espelha ScheduleAppointmentCommandHandler (agendamento feito pela equipe),
/// so trocando "cliente existente por Id" por "cliente resolvido/criado por
/// e-mail" — quem chama e um visitante do portal publico, sem cadastro previo
/// nem login (ver ICustomerRegistrationService).
/// </summary>
public sealed class PublicScheduleAppointmentCommandHandler(
    SchedulingDbContext dbContext,
    ITenantContext tenantContext,
    IClock clock,
    ICustomerRegistrationService customerRegistration,
    IResourceLookupService resourceLookup,
    IServiceLookupService serviceLookup,
    IBackgroundJobClient jobClient) : ICommandHandler<PublicScheduleAppointmentCommand, Guid>
{
    private const string ExclusionViolationSqlState = "23P01";

    public async Task<Result<Guid>> Handle(PublicScheduleAppointmentCommand request, CancellationToken cancellationToken)
    {
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

        Guid customerId;
        try
        {
            customerId = await customerRegistration.FindOrRegisterByEmailAsync(
                request.CustomerFullName, request.CustomerEmail, request.CustomerPhone, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(Error.Validation("Appointment.InvalidCustomerData", ex.Message));
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
                $"SELECT pg_advisory_xact_lock(hashtext({tenantContext.TenantId.Value.ToString()}), hashtext({request.ResourceId.ToString()}))",
                cancellationToken);

            var appointmentResult = Appointment.Schedule(
                tenantContext.TenantId, customerId, request.ResourceId, request.ServiceId,
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
