namespace Agendio.Infrastructure.Security;

/// <summary>
/// Configuracao da autoridade JWT do onboarding — issuer, audience e chave de
/// assinatura PROPRIOS, separados de <see cref="JwtOptions"/> (tenant) e
/// <see cref="PlatformJwtOptions"/> (Super Admin). Emitido no registro, antes de
/// existir sessao completa (e-mail ainda nao confirmado); prova posse do
/// TenantId recem-criado sem depender do cliente simplesmente afirmar um Guid
/// no corpo da requisicao (ver BL-01 do docs/BACKLOG.md).
/// </summary>
public sealed class OnboardingJwtOptions
{
    public const string SectionName = "OnboardingJwt";

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public required string SigningKey { get; init; }

    /// <summary>
    /// Precisa cobrir o tempo de escolher um plano pago e concluir o Checkout
    /// hospedado da Asaas (preencher cartao, etc.) — mais longo que o token de
    /// Platform (30min) por isso.
    /// </summary>
    public int AccessTokenLifetimeMinutes { get; init; } = 60;
}
