using Agendio.SharedKernel.DomainEvents;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Resources.Domain;

public sealed record ResourceCreatedDomainEvent(ResourceId ResourceId, TenantId TenantId, string Name) : DomainEvent;
