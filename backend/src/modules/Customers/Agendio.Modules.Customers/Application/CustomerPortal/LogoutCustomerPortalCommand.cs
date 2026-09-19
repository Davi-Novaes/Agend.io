using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Customers.Application.CustomerPortal;

public sealed record LogoutCustomerPortalCommand(Guid TenantId, string SessionToken) : ICommand, IHasExplicitTenant;
