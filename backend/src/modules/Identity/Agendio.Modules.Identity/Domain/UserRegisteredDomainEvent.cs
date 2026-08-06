using Agendio.Modules.Identity.Contracts;
using Agendio.SharedKernel.DomainEvents;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Identity.Domain;

public sealed record UserRegisteredDomainEvent(UserId UserId, TenantId TenantId, string Email) : DomainEvent;
