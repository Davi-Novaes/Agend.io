using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Identity.Application.RegisterUser;

/// <summary>TenantId vem do corpo da requisicao — ver IHasExplicitTenant (ancora o tenant antes de tocar o banco).</summary>
public sealed record RegisterUserCommand(Guid TenantId, string Email, string Password, string FullName)
    : ICommand<Guid>, IHasExplicitTenant;
