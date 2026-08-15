using Agendio.Modules.Financeiro.Domain;
using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.Modules.Resources.Contracts;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Financeiro.Application.GetCommissionReport;

public sealed class GetCommissionReportQueryHandler(FinanceiroDbContext dbContext, IResourceLookupService resourceLookupService)
    : IQueryHandler<GetCommissionReportQuery, IReadOnlyList<CommissionReportEntry>>
{
    public async Task<Result<IReadOnlyList<CommissionReportEntry>>> Handle(GetCommissionReportQuery request, CancellationToken cancellationToken)
    {
        // Cancelada nunca chega a ser paga nem fica pendente de pagamento —
        // fora do relatorio, mesmo raciocinio do resto do Financeiro (fluxo de
        // caixa so conta o realizado, nao o cancelado).
        var payables = await dbContext.AccountsPayable.AsNoTracking()
            .Where(a => a.Category == ExpenseCategory.Commission && a.ResourceId != null &&
                        a.Status != AccountPayableStatus.Cancelled && a.DueDate >= request.From && a.DueDate <= request.To)
            .Select(a => new { ResourceId = a.ResourceId!.Value, a.Status, a.Amount.Amount, a.Amount.Currency })
            .ToListAsync(cancellationToken);

        var grouped = payables
            .GroupBy(p => p.ResourceId)
            .Select(group => new
            {
                ResourceId = group.Key,
                PendingAmount = group.Where(p => p.Status == AccountPayableStatus.Pending).Sum(p => p.Amount),
                PaidAmount = group.Where(p => p.Status == AccountPayableStatus.Paid).Sum(p => p.Amount),
                Currency = group.First().Currency,
            })
            .OrderByDescending(group => group.PendingAmount + group.PaidAmount)
            .ToList();

        var entries = new List<CommissionReportEntry>();
        foreach (var group in grouped)
        {
            var resource = await resourceLookupService.FindByIdAsync(group.ResourceId, cancellationToken);
            entries.Add(new CommissionReportEntry(
                group.ResourceId, resource?.Name ?? "Profissional removido",
                group.PendingAmount, group.PaidAmount, group.PendingAmount + group.PaidAmount, group.Currency));
        }

        return Result.Success<IReadOnlyList<CommissionReportEntry>>(entries);
    }
}
