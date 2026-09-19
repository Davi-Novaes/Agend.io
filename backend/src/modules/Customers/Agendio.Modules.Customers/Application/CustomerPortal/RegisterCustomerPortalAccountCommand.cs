using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Customers.Application.CustomerPortal;

public sealed record RegisterCustomerPortalAccountCommand(Guid TenantId, string FullName, string Email, string? Phone)
    : ICommand, IHasExplicitTenant;
