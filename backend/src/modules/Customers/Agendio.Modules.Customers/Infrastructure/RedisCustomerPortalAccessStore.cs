using System.Security.Cryptography;
using System.Text;
using Agendio.Modules.Customers.Application.CustomerPortal;
using Agendio.SharedKernel.Time;
using StackExchange.Redis;

namespace Agendio.Modules.Customers.Infrastructure;

internal sealed class RedisCustomerPortalAccessStore(IConnectionMultiplexer redis, IClock clock)
    : ICustomerPortalAccessStore
{
    private const string KeyPrefix = "customer-portal";

    public async Task StoreChallengeAsync(
        Guid tenantId, string normalizedEmail, Guid customerId, string codeHash, TimeSpan lifetime)
    {
        var db = redis.GetDatabase();
        var key = ChallengeKey(tenantId, normalizedEmail);
        await db.HashSetAsync(key,
        [
            new HashEntry("code_hash", codeHash),
            new HashEntry("customer_id", customerId.ToString()),
            new HashEntry("attempts", 0),
        ]);
        await db.KeyExpireAsync(key, lifetime);
    }

    public async Task<Guid?> ConsumeChallengeAsync(Guid tenantId, string normalizedEmail, string codeHash)
    {
        const string script = """
            local current = redis.call('HGET', KEYS[1], 'code_hash')
            if not current then return false end
            local attempts = tonumber(redis.call('HGET', KEYS[1], 'attempts') or '0')
            if attempts >= 5 then redis.call('DEL', KEYS[1]); return false end
            if current == ARGV[1] then
              local customerId = redis.call('HGET', KEYS[1], 'customer_id')
              redis.call('DEL', KEYS[1])
              return customerId
            end
            attempts = attempts + 1
            if attempts >= 5 then redis.call('DEL', KEYS[1]) else redis.call('HSET', KEYS[1], 'attempts', attempts) end
            return false
            """;

        var result = await redis.GetDatabase().ScriptEvaluateAsync(
            script,
            [ChallengeKey(tenantId, normalizedEmail)],
            [codeHash]);

        return result.IsNull ? null : Guid.TryParse(result.ToString(), out var customerId) ? customerId : null;
    }

    public async Task<CustomerPortalSession> CreateSessionAsync(Guid tenantId, Guid customerId, TimeSpan lifetime)
    {
        var rawToken = ToBase64Url(RandomNumberGenerator.GetBytes(32));
        await redis.GetDatabase().StringSetAsync(SessionKey(tenantId, rawToken), customerId.ToString(), lifetime);
        return new CustomerPortalSession(rawToken, clock.UtcNow.Add(lifetime));
    }

    public async Task<Guid?> GetCustomerIdAsync(Guid tenantId, string rawToken)
    {
        var value = await redis.GetDatabase().StringGetAsync(SessionKey(tenantId, rawToken));
        return value.HasValue && Guid.TryParse(value.ToString(), out var customerId) ? customerId : null;
    }

    public Task RevokeSessionAsync(Guid tenantId, string rawToken) =>
        redis.GetDatabase().KeyDeleteAsync(SessionKey(tenantId, rawToken));

    private static string ChallengeKey(Guid tenantId, string normalizedEmail) =>
        $"{KeyPrefix}:challenge:{tenantId}:{Hash(normalizedEmail)}";

    private static string SessionKey(Guid tenantId, string rawToken) =>
        $"{KeyPrefix}:session:{tenantId}:{Hash(rawToken)}";

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
