namespace Agendio.SharedKernel.Security;

/// <summary>
/// Marca um Command cujo formulario de origem tem um widget Cloudflare
/// Turnstile — o TurnstileVerificationBehavior (Agendio.Infrastructure)
/// valida o token contra a API da Cloudflare antes do handler rodar, mesmo
/// padrao de IHasExplicitTenant (SharedKernel.Multitenancy).
/// </summary>
public interface IRequiresTurnstileVerification
{
    string TurnstileToken { get; }
}
