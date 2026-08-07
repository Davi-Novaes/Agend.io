namespace Agendio.Infrastructure.Security;

/// <summary>
/// Configuracao da autoridade JWT do Super Admin — issuer, audience e chave de
/// assinatura PROPRIOS, deliberadamente separados de <see cref="JwtOptions"/>.
/// Um token de tenant nunca valida aqui (issuer/audience/chave diferentes) e
/// vice-versa: comprometer um tenant nunca escala para a plataforma, e
/// comprometer a chave de um tenant (o SigningKey e por deploy, nao por tenant)
/// nunca compromete o Super Admin.
/// </summary>
public sealed class PlatformJwtOptions
{
    public const string SectionName = "PlatformJwt";

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public required string SigningKey { get; init; }

    /// <summary>Sem refresh token no MVP: sessao de admin e curta e o re-login e trivial (ver ADR do Sprint 6).</summary>
    public int AccessTokenLifetimeMinutes { get; init; } = 30;
}
