using Agendio.Infrastructure.Payments;
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
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Agendio.Modules.Scheduling.Application.PublicScheduleAppointment;

/// <summary>
/// Espelha ScheduleAppointmentCommandHandler (agendamento feito pela equipe),
/// so trocando "cliente existente por Id" por "cliente resolvido/criado por
/// e-mail" — quem chama e um visitante do portal publico, sem cadastro previo
/// nem login (ver ICustomerRegistrationService). Fase 16: se o tenant exige
/// sinal, gera a cobranca (Asaas/PIX) DEPOIS do agendamento ja commitado —
/// falha no gateway nunca desfaz um agendamento ja criado, so fica sem link
/// de pagamento (a equipe acompanha e pode gerar manualmente depois).
/// </summary>
public sealed class PublicScheduleAppointmentCommandHandler(
    SchedulingDbContext dbContext,
    ITenantContext tenantContext,
    IClock clock,
    ICustomerRegistrationService customerRegistration,
    IResourceLookupService resourceLookup,
    IServiceLookupService serviceLookup,
    ITenantLookupService tenantLookup,
    IPaymentChargeClient paymentChargeClient,
    IBackgroundJobClient jobClient,
    ILogger<PublicScheduleAppointmentCommandHandler> logger) : ICommandHandler<PublicScheduleAppointmentCommand, PublicScheduleAppointmentResult>
{
    private const string ExclusionViolationSqlState = "23P01";

    public async Task<Result<PublicScheduleAppointmentResult>> Handle(PublicScheduleAppointmentCommand request, CancellationToken cancellationToken)
    {
        var resource = await resourceLookup.FindByIdAsync(request.ResourceId, cancellationToken);
        if (resource is null || !resource.IsActive)
        {
            return Result.Failure<PublicScheduleAppointmentResult>(Error.NotFound("Appointment.ResourceNotFound", "Recurso nao encontrado ou inativo."));
        }

        var service = await serviceLookup.FindByIdAsync(request.ServiceId, cancellationToken);
        if (service is null || !service.IsActive)
        {
            return Result.Failure<PublicScheduleAppointmentResult>(Error.NotFound("Appointment.ServiceNotFound", "Servico nao encontrado ou inativo."));
        }

        if (request.StartAtUtc <= clock.UtcNow)
        {
            return Result.Failure<PublicScheduleAppointmentResult>(Error.Validation("Appointment.StartInThePast", "Nao e possivel agendar em um horario que ja passou."));
        }

        var paymentSettings = await tenantLookup.GetPaymentSettingsAsync(tenantContext.TenantId, cancellationToken);
        var paymentRequired = paymentSettings?.PaymentRequired ?? false;
        if (paymentRequired && string.IsNullOrWhiteSpace(request.CustomerCpf))
        {
            return Result.Failure<PublicScheduleAppointmentResult>(
                Error.Validation("Appointment.CpfRequired", "Este estabelecimento exige sinal para confirmar o agendamento — informe seu CPF."));
        }

        var availabilityCheck = await AppointmentAvailabilityGuard.EnsureResourceIsAvailableAsync(
            tenantLookup, resourceLookup, tenantContext.TenantId, request.ResourceId, request.StartAtUtc, cancellationToken);
        if (availabilityCheck.IsFailure)
        {
            return Result.Failure<PublicScheduleAppointmentResult>(availabilityCheck.Error);
        }

        var slotResult = TimeSlot.Create(request.StartAtUtc, request.StartAtUtc.AddMinutes(service.DurationMinutes));
        if (slotResult.IsFailure)
        {
            return Result.Failure<PublicScheduleAppointmentResult>(slotResult.Error);
        }

        var priceResult = Money.Create(service.Price, service.Currency);
        if (priceResult.IsFailure)
        {
            return Result.Failure<PublicScheduleAppointmentResult>(priceResult.Error);
        }

        var customerResult = await customerRegistration.FindOrRegisterByEmailAsync(
            request.CustomerFullName, request.CustomerEmail, request.CustomerPhone, request.CustomerCpf, cancellationToken);
        if (customerResult.IsFailure)
        {
            return Result.Failure<PublicScheduleAppointmentResult>(customerResult.Error);
        }

        var customerId = customerResult.Value;

        // Mesma tecnica de advisory lock usada em ScheduleAppointmentCommandHandler
        // — evita deadlock (40P01) entre transacoes concorrentes disputando o
        // indice GIST da EXCLUDE constraint no mesmo recurso.
        var strategy = dbContext.Database.CreateExecutionStrategy();

        var scheduleResult = await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({tenantContext.TenantId.Value.ToString()}), hashtext({request.ResourceId.ToString()}))",
                cancellationToken);

            var appointmentResult = Appointment.Schedule(
                tenantContext.TenantId, customerId, request.ResourceId, resource.UnitId, request.ServiceId,
                service.Name, slotResult.Value, priceResult.Value, request.Notes);

            if (appointmentResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<Appointment>(appointmentResult.Error);
            }

            dbContext.Appointments.Add(appointmentResult.Value);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: ExclusionViolationSqlState })
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<Appointment>(
                    Error.Conflict("Appointment.SlotTaken", "Esse horario acabou de ser reservado para este recurso. Escolha outro horario."));
            }

            await transaction.CommitAsync(cancellationToken);

            return Result.Success(appointmentResult.Value);
        });

        if (scheduleResult.IsFailure)
        {
            return Result.Failure<PublicScheduleAppointmentResult>(scheduleResult.Error);
        }

        var newAppointmentId = scheduleResult.Value.Id.Value;
        AppointmentNotificationScheduler.EnqueueConfirmation(jobClient, tenantContext.TenantId.Value, newAppointmentId);
        AppointmentNotificationScheduler.ScheduleReminders(jobClient, clock, tenantContext.TenantId.Value, newAppointmentId, slotResult.Value.StartUtc);

        string? paymentUrl = null;
        if (paymentRequired)
        {
            paymentUrl = await TryCreateDepositAsync(
                scheduleResult.Value.Id, priceResult.Value, paymentSettings!.DepositPercentage,
                request.CustomerFullName, request.CustomerCpf!, request.CustomerEmail, service.Name, cancellationToken);
        }

        return Result.Success(new PublicScheduleAppointmentResult(newAppointmentId, paymentUrl));
    }

    private async Task<string?> TryCreateDepositAsync(
        AppointmentId appointmentId, Money servicePrice, int depositPercentage,
        string customerName, string customerCpf, string customerEmail, string serviceName, CancellationToken cancellationToken)
    {
        var depositAmountResult = Money.Create(Math.Round(servicePrice.Amount * depositPercentage / 100m, 2), servicePrice.Currency);
        if (depositAmountResult.IsFailure)
        {
            logger.LogWarning("Nao foi possivel calcular o valor do sinal para o agendamento {AppointmentId}: {Error}", appointmentId.Value, depositAmountResult.Error.Message);
            return null;
        }

        var depositResult = AppointmentDeposit.Create(tenantContext.TenantId, appointmentId, depositAmountResult.Value);
        if (depositResult.IsFailure)
        {
            logger.LogWarning("Nao foi possivel criar o registro de sinal para o agendamento {AppointmentId}: {Error}", appointmentId.Value, depositResult.Error.Message);
            return null;
        }

        var deposit = depositResult.Value;
        dbContext.AppointmentDeposits.Add(deposit);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            // externalReference carrega tenantId+depositId — o webhook nao tem
            // JWT nem rota com tenantId, precisa reconstruir os dois a partir
            // do que a Asaas ecoa de volta (ver ProcessAppointmentDepositWebhookCommand).
            var externalReference = $"{tenantContext.TenantId.Value}:{deposit.Id.Value}";
            var charge = await paymentChargeClient.CreatePixChargeAsync(
                customerName, customerCpf, customerEmail, depositAmountResult.Value.Amount,
                $"Sinal - {serviceName}", externalReference, cancellationToken);

            deposit.AttachGatewayCharge(charge.ChargeId, charge.InvoiceUrl);
            await dbContext.SaveChangesAsync(cancellationToken);

            return charge.InvoiceUrl;
        }
        catch (Exception ex)
        {
            // O agendamento ja esta valido — a cobranca fica pendente sem link
            // (a equipe acompanha em /agenda e pode gerar manualmente depois).
            logger.LogWarning(ex, "Falha ao gerar cobranca de sinal via gateway para o agendamento {AppointmentId}.", appointmentId.Value);
            return null;
        }
    }
}
