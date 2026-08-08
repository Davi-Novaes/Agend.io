using Agendio.SharedKernel.DomainEvents;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Scheduling.Domain;

/// <summary>Consumido pelo modulo Financeiro para gerar a conta a receber (e, se houver regra de comissao, a conta a pagar) do atendimento.</summary>
public sealed record AppointmentCompletedDomainEvent(
    AppointmentId AppointmentId, TenantId TenantId, Guid ResourceId, string ServiceName, Money Price, DateTimeOffset CompletedAtUtc) : DomainEvent;
