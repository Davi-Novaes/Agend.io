namespace Agendio.Modules.Customers.Application.CustomerPortal;

public interface ICustomerPortalAccessStore
{
    Task StoreChallengeAsync(Guid tenantId, string normalizedEmail, Guid customerId, string codeHash, TimeSpan lifetime);

    Task<Guid?> ConsumeChallengeAsync(Guid tenantId, string normalizedEmail, string codeHash);

    Task<CustomerPortalSession> CreateSessionAsync(Guid tenantId, Guid customerId, TimeSpan lifetime);

    Task<Guid?> GetCustomerIdAsync(Guid tenantId, string rawToken);

    Task RevokeSessionAsync(Guid tenantId, string rawToken);
}

public sealed record CustomerPortalSession(string RawToken, DateTimeOffset ExpiresAtUtc);
