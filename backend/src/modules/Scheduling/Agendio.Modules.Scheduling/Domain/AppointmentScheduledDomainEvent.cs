using Agendio.SharedKernel.DomainEvents;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Scheduling.Domain;

/// <summary>Consumido futuramente para invalidar o cache de disponibilidade (Redis) do par recurso+dia.</summary>
public sealed record AppointmentScheduledDomainEvent(
    AppointmentId AppointmentId, TenantId TenantId, Guid ResourceId, DateTimeOffset StartUtc, DateTimeOffset EndUtc) : DomainEvent;
