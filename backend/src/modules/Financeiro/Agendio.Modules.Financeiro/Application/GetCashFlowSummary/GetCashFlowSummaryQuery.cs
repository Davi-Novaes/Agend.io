using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Financeiro.Application.GetCashFlowSummary;

// Reflete caixa REALIZADO (contas ja Received/Paid), nao projetado — contas
// Pending aparecem nas telas de Contas a Receber/Pagar, nao aqui.
public sealed record GetCashFlowSummaryQuery(DateOnly From, DateOnly To) : IQuery<CashFlowSummary>;

public sealed record CashFlowMonthPoint(string Month, decimal Received, decimal Paid);

public sealed record CashFlowCategoryPoint(string Category, decimal Total);

public sealed record CashFlowSummary(
    decimal TotalReceived,
    decimal TotalPaid,
    decimal NetBalance,
    IReadOnlyList<CashFlowMonthPoint> SeriesByMonth,
    IReadOnlyList<CashFlowCategoryPoint> CategoryBreakdown);
