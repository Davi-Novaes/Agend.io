namespace Agendio.Modules.Scheduling.Contracts;

/// <summary>
/// Leitura dos agendamentos pertencentes a um cliente autenticado no portal.
/// O tenant continua vindo do contexto ambiente e dos filtros globais/RLS.
/// </summary>
public interface ICustomerPortalAppointmentsLookupService
{
    Task<IReadOnlyList<CustomerPortalAppointmentLookupResult>> ListForCustomerAsync(
        Guid customerId, CancellationToken cancellationToken = default);
}

public sealed record CustomerPortalAppointmentLookupResult(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    string ResourceName,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    decimal Price,
    string Currency,
    string Status);
