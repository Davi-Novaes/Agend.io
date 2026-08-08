using System.Globalization;
using Agendio.Modules.Financeiro.Domain;
using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Financeiro.Application.GetCashFlowSummary;

public sealed class GetCashFlowSummaryQueryHandler(FinanceiroDbContext dbContext) : IQueryHandler<GetCashFlowSummaryQuery, CashFlowSummary>
{
    public async Task<Result<CashFlowSummary>> Handle(GetCashFlowSummaryQuery request, CancellationToken cancellationToken)
    {
        var fromUtc = new DateTimeOffset(request.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtcExclusive = new DateTimeOffset(request.To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var received = await dbContext.AccountsReceivable.AsNoTracking()
            .Where(a => a.Status == AccountReceivableStatus.Received && a.ReceivedAtUtc >= fromUtc && a.ReceivedAtUtc < toUtcExclusive)
            .Select(a => new { Month = a.ReceivedAtUtc!.Value.Month, Year = a.ReceivedAtUtc!.Value.Year, a.Amount.Amount })
            .ToListAsync(cancellationToken);

        var paid = await dbContext.AccountsPayable.AsNoTracking()
            .Where(a => a.Status == AccountPayableStatus.Paid && a.PaidAtUtc >= fromUtc && a.PaidAtUtc < toUtcExclusive)
            .Select(a => new { Month = a.PaidAtUtc!.Value.Month, Year = a.PaidAtUtc!.Value.Year, a.Amount.Amount, a.Category })
            .ToListAsync(cancellationToken);

        var totalReceived = received.Sum(r => r.Amount);
        var totalPaid = paid.Sum(p => p.Amount);

        var months = received.Select(r => (r.Year, r.Month))
            .Concat(paid.Select(p => (p.Year, p.Month)))
            .Distinct()
            .OrderBy(point => point.Year).ThenBy(point => point.Month);

        var seriesByMonth = months
            .Select(point => new CashFlowMonthPoint(
                FormatMonth(point.Year, point.Month),
                received.Where(r => r.Year == point.Year && r.Month == point.Month).Sum(r => r.Amount),
                paid.Where(p => p.Year == point.Year && p.Month == point.Month).Sum(p => p.Amount)))
            .ToList();

        var categoryBreakdown = paid
            .GroupBy(p => p.Category)
            .Select(group => new CashFlowCategoryPoint(group.Key.ToString(), group.Sum(p => p.Amount)))
            .OrderByDescending(point => point.Total)
            .ToList();

        var summary = new CashFlowSummary(totalReceived, totalPaid, totalReceived - totalPaid, seriesByMonth, categoryBreakdown);
        return Result.Success(summary);
    }

    private static string FormatMonth(int year, int month) =>
        string.Create(CultureInfo.InvariantCulture, $"{year:D4}-{month:D2}");
}
