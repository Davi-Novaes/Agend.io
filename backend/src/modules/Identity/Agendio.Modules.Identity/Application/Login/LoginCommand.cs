using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Security;

namespace Agendio.Modules.Identity.Application.Login;

// TurnstileToken nao tem regra NotEmpty no validator de proposito: exigir
// isso quebraria todo teste/chamador que nao passa por um widget real. A
// obrigatoriedade e condicional — so entra em vigor quando Turnstile:SecretKey
// esta configurado (ver TurnstileVerificationBehavior/CloudflareTurnstileVerifier),
// que e justamente o caso em que um token vazio deveria mesmo falhar.
public sealed record LoginCommand(Guid TenantId, string Email, string Password, string TurnstileToken)
    : ICommand<LoginResult>, IHasExplicitTenant, IRequiresTurnstileVerification;
