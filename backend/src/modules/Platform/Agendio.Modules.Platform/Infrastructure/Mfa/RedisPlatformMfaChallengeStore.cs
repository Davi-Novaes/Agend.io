using System.Text.Json;
using Agendio.Modules.Platform.Domain;
using Microsoft.Extensions.Caching.Distributed;

namespace Agendio.Modules.Platform.Infrastructure.Mfa;

public sealed class RedisPlatformMfaChallengeStore(IDistributedCache cache) : IPlatformMfaChallengeStore
{
    public Task CreateAsync(string challengeToken, PlatformAdminId adminId, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new StoredChallenge(adminId.Value));

        return cache.SetStringAsync(
            BuildKey(challengeToken),
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAtUtc },
            cancellationToken);
    }

    public async Task<PlatformMfaChallenge?> ConsumeAsync(string challengeToken, CancellationToken cancellationToken)
    {
        var key = BuildKey(challengeToken);
        var payload = await cache.GetStringAsync(key, cancellationToken);

        if (payload is null)
        {
            return null;
        }

        // Uso unico — mesmo raciocinio de RedisMfaChallengeStore (Identity).
        await cache.RemoveAsync(key, cancellationToken);

        var stored = JsonSerializer.Deserialize<StoredChallenge>(payload)!;
        return new PlatformMfaChallenge(PlatformAdminId.From(stored.AdminId));
    }

    private static string BuildKey(string challengeToken) => $"platform-mfa-challenge:{challengeToken}";

    private sealed record StoredChallenge(Guid AdminId);
}
