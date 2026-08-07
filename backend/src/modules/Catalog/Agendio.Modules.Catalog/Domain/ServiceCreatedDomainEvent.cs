using Agendio.SharedKernel.DomainEvents;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Catalog.Domain;

public sealed record ServiceCreatedDomainEvent(ServiceId ServiceId, TenantId TenantId, string Name) : DomainEvent;
