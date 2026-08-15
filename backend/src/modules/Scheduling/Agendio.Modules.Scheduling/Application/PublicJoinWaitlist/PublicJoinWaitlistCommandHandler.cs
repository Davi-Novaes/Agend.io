using Agendio.Modules.Catalog.Contracts;
using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;

namespace Agendio.Modules.Scheduling.Application.PublicJoinWaitlist;

/// <summary>
/// Espelha PublicScheduleAppointmentCommandHandler para a parte de resolver-ou-criar
/// o cliente pelo e-mail — a diferenca e que aqui nao ha horario para reservar,
/// so uma intencao registrada para quando uma vaga compativel abrir (ver
/// CancelAppointmentCommandHandler).
/// </summary>
public sealed class PublicJoinWaitlistCommandHandler(
    SchedulingDbContext dbContext,
    ITenantContext tenantContext,
    IClock clock,
    ICustomerRegistrationService customerRegistration,
    IResourceLookupService resourceLookup,
    IServiceLookupService serviceLookup) : ICommandHandler<PublicJoinWaitlistCommand, Guid>
{
    public async Task<Result<Guid>> Handle(PublicJoinWaitlistCommand request, CancellationToken cancellationToken)
    {
        var service = await serviceLookup.FindByIdAsync(request.ServiceId, cancellationToken);
        if (service is null || !service.IsActive)
        {
            return Result.Failure<Guid>(Error.NotFound("Waitlist.ServiceNotFound", "Servico nao encontrado ou inativo."));
        }

        if (request.ResourceId is { } resourceId)
        {
            var resource = await resourceLookup.FindByIdAsync(resourceId, cancellationToken);
            if (resource is null || !resource.IsActive)
            {
                return Result.Failure<Guid>(Error.NotFound("Waitlist.ResourceNotFound", "Recurso nao encontrado ou inativo."));
            }
        }

        // Tolerancia de 1 dia: o seletor de data usa o fuso LOCAL do navegador do
        // cliente (mesmo campo de data do agendamento publico), enquanto "hoje"
        // aqui e UTC — perto da meia-noite UTC, um cliente em fuso negativo (ex.:
        // Brasil) ve uma data ainda valida no proprio calendario que o servidor,
        // em UTC, já considera "passada". Rejeitar de verdade so a partir de 2
        // dias atras evita falso-negativo nesse limite sem abrir mao da checagem.
        var earliestAllowedDate = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime).AddDays(-1);
        if (request.PreferredDate < earliestAllowedDate)
        {
            return Result.Failure<Guid>(Error.Validation("Waitlist.PreferredDateInThePast", "A data desejada ja passou."));
        }

        var customerResult = await customerRegistration.FindOrRegisterByEmailAsync(
            request.CustomerFullName, request.CustomerEmail, request.CustomerPhone, cancellationToken: cancellationToken);
        if (customerResult.IsFailure)
        {
            return Result.Failure<Guid>(customerResult.Error);
        }

        var entryResult = WaitlistEntry.Create(
            tenantContext.TenantId, customerResult.Value, request.ResourceId, request.ServiceId, service.Name, request.PreferredDate,
            request.Notes);
        if (entryResult.IsFailure)
        {
            return Result.Failure<Guid>(entryResult.Error);
        }

        dbContext.WaitlistEntries.Add(entryResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(entryResult.Value.Id.Value);
    }
}
