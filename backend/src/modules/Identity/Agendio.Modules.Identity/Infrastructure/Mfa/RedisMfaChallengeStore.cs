using System.Text.Json;
using Agendio.Modules.Identity.Contracts;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.Extensions.Caching.Distributed;

namespace Agendio.Modules.Identity.Infrastructure.Mfa;

public sealed class RedisMfaChallengeStore(IDistributedCache cache) : IMfaChallengeStore
{
    public Task CreateAsync(string challengeToken, UserId userId, TenantId tenantId, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new StoredChallenge(userId.Value, tenantId.Value));

        return cache.SetStringAsync(
            BuildKey(challengeToken),
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAtUtc },
            cancellationToken);
    }

    public async Task<MfaChallenge?> ConsumeAsync(string challengeToken, CancellationToken cancellationToken)
    {
        var key = BuildKey(challengeToken);
        var payload = await cache.GetStringAsync(key, cancellationToken);

        if (payload is null)
        {
            return null;
        }

        // Uso unico: apaga assim que le, sucesso ou nao o codigo apresentado
        // depois — reapresentar o MESMO challenge token nunca funciona duas vezes.
        await cache.RemoveAsync(key, cancellationToken);

        var stored = JsonSerializer.Deserialize<StoredChallenge>(payload)!;
        return new MfaChallenge(UserId.From(stored.UserId), TenantId.From(stored.TenantId));
    }

    private static string BuildKey(string challengeToken) => $"mfa-challenge:{challengeToken}";

    private sealed record StoredChallenge(Guid UserId, Guid TenantId);
}
