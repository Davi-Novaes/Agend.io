using Agendio.Modules.Platform.Domain;

namespace Agendio.Modules.Platform.Infrastructure.Mfa;

public sealed record PlatformMfaChallenge(PlatformAdminId AdminId);

/// <summary>Mesmo papel de Identity.Infrastructure.Mfa.IMfaChallengeStore, sem TenantId (PlatformAdmin fica fora de qualquer tenant) — ver o comentario la para o raciocinio completo.</summary>
public interface IPlatformMfaChallengeStore
{
    Task CreateAsync(string challengeToken, PlatformAdminId adminId, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken);

    Task<PlatformMfaChallenge?> ConsumeAsync(string challengeToken, CancellationToken cancellationToken);
}
