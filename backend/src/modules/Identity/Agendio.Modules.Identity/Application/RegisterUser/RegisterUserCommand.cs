using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Identity.Application.RegisterUser;

/// <summary>
/// TenantId vem do corpo da requisicao — ver IHasExplicitTenant (ancora o
/// tenant antes de tocar o banco). Phone/CpfCnpj/TermsAccepted sao do dono/
/// responsavel pelo estabelecimento — obrigatorios (ver RegisterUserCommandValidator).
/// </summary>
public sealed record RegisterUserCommand(
    Guid TenantId, string Email, string Password, string FullName, string Phone, string CpfCnpj, bool TermsAccepted)
    : ICommand<RegisterUserResult>, IHasExplicitTenant;

/// <summary>
/// OnboardingToken prova posse do TenantId recem-criado para o resto do
/// onboarding (escolha de plano) sem exigir e-mail confirmado nem reintroduzir
/// TenantId "so afirmado" no corpo de outra requisicao — ver BL-01 em
/// docs/BACKLOG.md.
/// </summary>
public sealed record RegisterUserResult(Guid UserId, string OnboardingToken, DateTimeOffset OnboardingTokenExpiresAtUtc);
