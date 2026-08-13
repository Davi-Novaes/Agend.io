using Agendio.Modules.Resources.Contracts;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Scheduling.Application.Shared;

/// <summary>
/// Checagem de "o recurso pode ser agendado nessa data" (data fechada do
/// estabelecimento, folga do recurso) — compartilhada entre os tres pontos
/// que confirmam um agendamento (ScheduleAppointment, PublicScheduleAppointment,
/// RescheduleAppointment). Defesa em profundidade contra a corrida entre
/// listar disponibilidade (GetAvailableSlotsQueryHandler, que ja aplica a
/// mesma regra) e confirmar — ver Fase 4.
/// </summary>
internal static class AppointmentAvailabilityGuard
{
    public static async Task<Result> EnsureResourceIsAvailableAsync(
        ITenantLookupService tenantLookup,
        IResourceLookupService resourceLookup,
        TenantId tenantId,
        Guid resourceId,
        DateTimeOffset startAtUtc,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantLookup.GetAvailabilityInfoAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure(Error.NotFound("Appointment.TenantNotFound", "Estabelecimento nao encontrado."));
        }

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(tenant.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return Result.Failure(Error.Failure("Appointment.InvalidTimeZone", "Fuso horario do estabelecimento invalido."));
        }

        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startAtUtc.UtcDateTime, timeZone));

        if (tenant.ClosedDates.Contains(localDate))
        {
            return Result.Failure(Error.Conflict("Appointment.EstablishmentClosed", "O estabelecimento nao atende nessa data."));
        }

        var timeOff = await resourceLookup.ListTimeOffAsync(resourceId, localDate, localDate, cancellationToken);
        if (timeOff.Count > 0)
        {
            return Result.Failure(Error.Conflict("Appointment.ResourceOnTimeOff", "O profissional esta de folga nessa data."));
        }

        return Result.Success();
    }
}
