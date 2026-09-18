using StackExchange.Redis;

namespace Agendio.Infrastructure.Security;

/// <summary>
/// INCR+EXPIRE atomico via StackExchange.Redis direto (nao IDistributedCache —
/// aquela abstracao nao expoe incremento atomico, so Get/Set, que teria uma
/// corrida classica entre ler e escrever de volta).
/// </summary>
public sealed class RedisEmailSendThrottle(IConnectionMultiplexer redis) : IEmailSendThrottle
{
    private const string KeyPrefix = "email-throttle:";

    public async Task<bool> TryConsumeAsync(string key, int maxAttempts, TimeSpan window, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var redisKey = $"{KeyPrefix}{key}";

        var count = await db.StringIncrementAsync(redisKey);
        if (count == 1)
        {
            // So a chamada que criou a chave define o TTL — evita que uma
            // corrida entre incrementos concorrentes reinicie a janela.
            await db.KeyExpireAsync(redisKey, window);
        }

        return count <= maxAttempts;
    }
}
