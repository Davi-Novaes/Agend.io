using Agendio.Modules.Identity.Contracts;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Identity.Infrastructure.Mfa;

public sealed record MfaChallenge(UserId UserId, TenantId TenantId);

/// <summary>
/// Estado do desafio de MFA entre "senha validou" e "codigo TOTP confirmou" —
/// vive so em Redis (5 min, uso unico), nunca no Postgres. Token opaco de alta
/// entropia como identificador (nao um JWT): precisa de revogacao no primeiro
/// uso, que um JWT stateless nao da sem um denylist extra — ver ADR 0008.
/// </summary>
public interface IMfaChallengeStore
{
    Task CreateAsync(string challengeToken, UserId userId, TenantId tenantId, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken);

    /// <summary>Le e IMEDIATAMENTE apaga a entrada (uso unico) — null se o token nao existe ou ja expirou.</summary>
    Task<MfaChallenge?> ConsumeAsync(string challengeToken, CancellationToken cancellationToken);
}
