using Agendio.Modules.Financeiro.Application.GetCashFlowSummary;
using Agendio.Modules.Financeiro.Application.GetCommissionReport;
using Agendio.Modules.Financeiro.Contracts;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Financeiro.Infrastructure;

// Delega para os handlers ja existentes via IDispatcher (chamada intra-modulo,
// permitida) em vez de duplicar a logica de agregacao aqui.
public sealed class FinanceSummaryLookupService(IDispatcher dispatcher) : IFinanceSummaryLookupService
{
    public async Task<CashFlowSummaryLookupResult> GetCashFlowSummaryAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.Query(new GetCashFlowSummaryQuery(from, to), cancellationToken);
        var summary = result.Value;

        return new CashFlowSummaryLookupResult(
            summary.TotalReceived,
            summary.TotalPaid,
            summary.NetBalance,
            summary.SeriesByMonth.Select(p => new CashFlowMonthLookupPoint(p.Month, p.Received, p.Paid)).ToList(),
            summary.CategoryBreakdown.Select(p => new CashFlowCategoryLookupPoint(p.Category, p.Total)).ToList());
    }

    public async Task<IReadOnlyList<CommissionReportLookupEntry>> GetCommissionReportAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var result = await dispatcher.Query(new GetCommissionReportQuery(from, to), cancellationToken);

        return result.Value
            .Select(e => new CommissionReportLookupEntry(e.ResourceId, e.ResourceName, e.PendingAmount, e.PaidAmount, e.TotalAmount, e.Currency))
            .ToList();
    }
}
