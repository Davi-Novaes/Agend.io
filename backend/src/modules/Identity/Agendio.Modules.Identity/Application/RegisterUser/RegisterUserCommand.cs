using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Identity.Application.RegisterUser;

/// <summary>TenantId vem do corpo da requisicao — ver IHasExplicitTenant (ancora o tenant antes de tocar o banco).</summary>
public sealed record RegisterUserCommand(Guid TenantId, string Email, string Password, string FullName)
    : ICommand<RegisterUserResult>, IHasExplicitTenant;

/// <summary>
/// OnboardingToken prova posse do TenantId recem-criado para o resto do
/// onboarding (escolha de plano) sem exigir e-mail confirmado nem reintroduzir
/// TenantId "so afirmado" no corpo de outra requisicao — ver BL-01 em
/// docs/BACKLOG.md.
/// </summary>
public sealed record RegisterUserResult(Guid UserId, string OnboardingToken, DateTimeOffset OnboardingTokenExpiresAtUtc);
