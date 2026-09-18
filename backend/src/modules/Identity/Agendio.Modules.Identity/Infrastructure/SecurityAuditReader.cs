using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Identity.Infrastructure;

public sealed class SecurityAuditReader(IdentityDbContext dbContext) : ISecurityAuditReader
{
    public async Task<IReadOnlyList<SecurityAuditEvent>> GetRecentEventsAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken = default) =>
        await dbContext.SecurityAuditLog
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= sinceUtc)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Select(e => new SecurityAuditEvent(
                e.Id, e.TenantId, e.ActorId, e.EventType, e.Success, e.IpAddress, e.CountryCode, e.Region, e.City, e.UserAgent, e.Metadata, e.OccurredAtUtc))
            .ToListAsync(cancellationToken);
}
