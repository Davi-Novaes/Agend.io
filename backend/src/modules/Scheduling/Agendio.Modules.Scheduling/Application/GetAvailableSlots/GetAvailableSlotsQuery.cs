using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Scheduling.Application.GetAvailableSlots;

/// <summary>TenantId vem da rota publica — ver IHasExplicitTenant.</summary>
public sealed record GetAvailableSlotsQuery(Guid TenantId, Guid ResourceId, Guid ServiceId, DateOnly Date)
    : IQuery<IReadOnlyList<AvailableSlot>>, IHasExplicitTenant;

public sealed record AvailableSlot(DateTimeOffset StartUtc, DateTimeOffset EndUtc);
