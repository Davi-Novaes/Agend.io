using Agendio.Modules.Identity.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;

namespace Agendio.Modules.Identity.Application.GetMySecurityActivity;

// Janela de 90 dias e limite de 50 entradas: e o historico da PROPRIA conta
// (nao o painel do Super Admin, que olha todos os tenants) -- o objetivo e
// "o que aconteceu com meu login recentemente", nao um audit trail completo.
public sealed class GetMySecurityActivityQueryHandler(ISecurityAuditReader securityAuditReader, IClock clock)
    : IQueryHandler<GetMySecurityActivityQuery, IReadOnlyList<MySecurityActivityEntry>>
{
    private const int MaxEntries = 50;
    private static readonly TimeSpan LookbackWindow = TimeSpan.FromDays(90);

    public async Task<Result<IReadOnlyList<MySecurityActivityEntry>>> Handle(GetMySecurityActivityQuery request, CancellationToken cancellationToken)
    {
        var events = await securityAuditReader.GetRecentEventsAsync(clock.UtcNow - LookbackWindow, cancellationToken);

        var entries = events
            .Where(e => e.ActorId == request.UserId)
            .Take(MaxEntries)
            .Select(e => new MySecurityActivityEntry(
                e.Id, e.EventType, e.Success, e.IpAddress, e.CountryCode, e.Region, e.City, e.UserAgent, e.OccurredAtUtc))
            .ToList();

        return Result.Success<IReadOnlyList<MySecurityActivityEntry>>(entries);
    }
}
