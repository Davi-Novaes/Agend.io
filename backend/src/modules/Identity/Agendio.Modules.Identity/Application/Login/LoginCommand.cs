using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Identity.Application.Login;

public sealed record LoginCommand(Guid TenantId, string Email, string Password)
    : ICommand<AuthTokensResult>, IHasExplicitTenant;
