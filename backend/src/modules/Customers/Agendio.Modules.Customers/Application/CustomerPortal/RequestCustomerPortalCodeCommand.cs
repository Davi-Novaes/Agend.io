using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Customers.Application.CustomerPortal;

public sealed record RequestCustomerPortalCodeCommand(Guid TenantId, string Email) : ICommand, IHasExplicitTenant;
