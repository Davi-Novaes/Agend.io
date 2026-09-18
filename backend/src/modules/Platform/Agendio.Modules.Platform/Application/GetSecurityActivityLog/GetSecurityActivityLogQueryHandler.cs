using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Platform.Infrastructure.Persistence;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Platform.Application.GetSecurityActivityLog;

public sealed class GetSecurityActivityLogQueryHandler(
    PlatformDbContext dbContext,
    ISecurityAuditReader identitySecurityAuditReader,
    ITenantAdministrationService tenantAdministrationService,
    IClock clock) : IQueryHandler<GetSecurityActivityLogQuery, IReadOnlyList<SecurityActivityEntry>>
{
    private const int LookbackDays = 14;
    private const int MaxEntries = 300;

    public async Task<Result<IReadOnlyList<SecurityActivityEntry>>> Handle(GetSecurityActivityLogQuery request, CancellationToken cancellationToken)
    {
        var sinceUtc = clock.UtcNow.AddDays(-LookbackDays);

        var identityEvents = await identitySecurityAuditReader.GetRecentEventsAsync(sinceUtc, cancellationToken);
        var platformEvents = await dbContext.SecurityAuditLog
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= sinceUtc)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(MaxEntries)
            .ToListAsync(cancellationToken);

        // So os tenants sao resolvidos para nome (evento de Platform nunca tem
        // TenantId) — ActorId (UserId/PlatformAdminId) fica so como referencia
        // interna, sem lookup: e-mail nunca foi gravado no log de proposito
        // (ver comentario de LoginCommandHandler.LogAsync).
        var tenants = await tenantAdministrationService.ListAllAsync(cancellationToken);
        var tenantNameById = tenants.ToDictionary(t => t.TenantId, t => t.Name);

        var merged = identityEvents
            .Select(e => new SecurityActivityEntry(
                e.Id, "Identity",
                e.TenantId is { } tenantId && tenantNameById.TryGetValue(TenantId.From(tenantId), out var name) ? name : null,
                e.EventType, e.Success, e.IpAddress, e.CountryCode, e.Region, e.City, e.UserAgent, e.Metadata, e.OccurredAtUtc))
            .Concat(platformEvents.Select(e => new SecurityActivityEntry(
                e.Id, "Platform", null, e.EventType, e.Success, e.IpAddress, e.CountryCode, e.Region, e.City, e.UserAgent, e.Metadata, e.OccurredAtUtc)))
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(MaxEntries)
            .ToList();

        return Result.Success<IReadOnlyList<SecurityActivityEntry>>(merged);
    }
}
