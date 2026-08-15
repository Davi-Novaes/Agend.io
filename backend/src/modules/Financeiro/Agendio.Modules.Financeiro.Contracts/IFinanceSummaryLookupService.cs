namespace Agendio.Modules.Financeiro.Contracts;

/// <summary>
/// Leitura agregada do caixa e comissoes — usada pelo Assistente (Fase 22) para
/// responder perguntas em linguagem natural sem o modulo Assistant precisar ler
/// tabela do Financeiro diretamente.
/// </summary>
public interface IFinanceSummaryLookupService
{
    Task<CashFlowSummaryLookupResult> GetCashFlowSummaryAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommissionReportLookupEntry>> GetCommissionReportAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public sealed record CashFlowMonthLookupPoint(string Month, decimal Received, decimal Paid);

public sealed record CashFlowCategoryLookupPoint(string Category, decimal Total);

public sealed record CashFlowSummaryLookupResult(
    decimal TotalReceived,
    decimal TotalPaid,
    decimal NetBalance,
    IReadOnlyList<CashFlowMonthLookupPoint> SeriesByMonth,
    IReadOnlyList<CashFlowCategoryLookupPoint> CategoryBreakdown);

public sealed record CommissionReportLookupEntry(
    Guid ResourceId, string ResourceName, decimal PendingAmount, decimal PaidAmount, decimal TotalAmount, string Currency);
