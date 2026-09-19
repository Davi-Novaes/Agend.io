using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Customers.Application.CustomerPortal;

public sealed record VerifyCustomerPortalCodeCommand(Guid TenantId, string Email, string Code)
    : ICommand<CustomerPortalSession>, IHasExplicitTenant;
