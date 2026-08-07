using Agendio.SharedKernel.DomainEvents;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Customers.Domain;

public sealed record CustomerCreatedDomainEvent(CustomerId CustomerId, TenantId TenantId, string FullName) : DomainEvent;
