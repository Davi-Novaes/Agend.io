using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Financeiro.Application.GetCommissionReport;

public sealed record GetCommissionReportQuery(DateOnly From, DateOnly To) : IQuery<IReadOnlyList<CommissionReportEntry>>;

public sealed record CommissionReportEntry(
    Guid ResourceId, string ResourceName, decimal PendingAmount, decimal PaidAmount, decimal TotalAmount, string Currency);
