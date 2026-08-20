using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Scheduling.Domain;
using Agendio.Modules.Scheduling.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Scheduling.Application.GetCustomerRecoveryCandidates;

/// <summary>
/// Fase 10 — recuperacao de clientes. "Intervalo habitual" e pessoal de cada
/// cliente (media dos intervalos entre visitas concluidas consecutivas), nao
/// um numero fixo pro tenant inteiro — dois clientes do mesmo salao podem ter
/// cadencias bem diferentes (corte mensal vs. tintura trimestral). Precisa de
/// pelo menos <see cref="MinimumCompletedVisits"/> visitas concluidas pra ter
/// uma media que signifique alguma coisa. Cliente com agendamento futuro ja
/// marcado nao entra na lista — ja resolveu sozinho.
/// </summary>
public sealed class GetCustomerRecoveryCandidatesQueryHandler(
    SchedulingDbContext dbContext, ICustomerLookupService customerLookup, IClock clock)
    : IQueryHandler<GetCustomerRecoveryCandidatesQuery, IReadOnlyList<CustomerRecoveryCandidate>>
{
    private const int MinimumCompletedVisits = 2;
    private const int MinimumDaysOverdue = 3;

    public async Task<Result<IReadOnlyList<CustomerRecoveryCandidate>>> Handle(
        GetCustomerRecoveryCandidatesQuery request, CancellationToken cancellationToken)
    {
        var nowUtc = clock.UtcNow;

        var completedVisits = await dbContext.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Completed)
            .Select(a => new { a.CustomerId, a.Slot.StartUtc })
            .ToListAsync(cancellationToken);

        var futureBookedCustomerIds = await dbContext.Appointments.AsNoTracking()
            .Where(a => (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed) && a.Slot.StartUtc > nowUtc)
            .Select(a => a.CustomerId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var futureBookedSet = futureBookedCustomerIds.ToHashSet();

        var overdueByCustomerId = new Dictionary<Guid, (int AverageIntervalDays, int DaysSinceLastVisit, int DaysOverdue, DateTimeOffset LastVisitAtUtc)>();

        foreach (var group in completedVisits.GroupBy(v => v.CustomerId))
        {
            if (futureBookedSet.Contains(group.Key))
            {
                continue;
            }

            var visitDates = group.Select(v => v.StartUtc).OrderBy(d => d).ToList();
            if (visitDates.Count < MinimumCompletedVisits)
            {
                continue;
            }

            var gaps = new List<double>();
            for (var i = 1; i < visitDates.Count; i++)
            {
                gaps.Add((visitDates[i] - visitDates[i - 1]).TotalDays);
            }

            var averageIntervalDays = Math.Max(1, (int)Math.Round(gaps.Average()));
            var lastVisitAtUtc = visitDates[^1];
            var daysSinceLastVisit = (int)(nowUtc - lastVisitAtUtc).TotalDays;
            var daysOverdue = daysSinceLastVisit - averageIntervalDays;

            if (daysOverdue >= MinimumDaysOverdue)
            {
                overdueByCustomerId[group.Key] = (averageIntervalDays, daysSinceLastVisit, daysOverdue, lastVisitAtUtc);
            }
        }

        // Busca em lote (uma query) em vez de uma por candidato — BL-20, docs/BACKLOG.md.
        var customersById = (await customerLookup.FindByIdsAsync(overdueByCustomerId.Keys, cancellationToken))
            .ToDictionary(c => c.CustomerId);

        var results = new List<CustomerRecoveryCandidate>();
        foreach (var (customerId, stats) in overdueByCustomerId.OrderByDescending(kv => kv.Value.DaysOverdue))
        {
            if (!customersById.TryGetValue(customerId, out var customer) || !customer.IsActive)
            {
                continue;
            }

            results.Add(new CustomerRecoveryCandidate(
                customerId, customer.FullName, customer.Email,
                stats.AverageIntervalDays, stats.DaysSinceLastVisit, stats.DaysOverdue, stats.LastVisitAtUtc));
        }

        return Result.Success<IReadOnlyList<CustomerRecoveryCandidate>>(results);
    }
}
