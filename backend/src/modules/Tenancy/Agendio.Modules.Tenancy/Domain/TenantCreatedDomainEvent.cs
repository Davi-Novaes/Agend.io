using Agendio.SharedKernel.DomainEvents;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Tenancy.Domain;

public sealed record TenantCreatedDomainEvent(TenantId TenantId, string Name, string Slug) : DomainEvent;
