using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Financeiro.Application.ListAccountsPayable;

public sealed class ListAccountsPayableQueryHandler(FinanceiroDbContext dbContext)
    : IQueryHandler<ListAccountsPayableQuery, ListAccountsPayableResult>
{
    public async Task<Result<ListAccountsPayableResult>> Handle(ListAccountsPayableQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        // O Global Query Filter ja restringe isto ao tenant do JWT do chamador.
        var query = dbContext.AccountsPayable.AsNoTracking().AsQueryable();

        if (request.Status is not null)
        {
            query = query.Where(a => a.Status == request.Status);
        }

        if (request.Category is not null)
        {
            query = query.Where(a => a.Category == request.Category);
        }

        if (request.From is not null)
        {
            query = query.Where(a => a.DueDate >= request.From);
        }

        if (request.To is not null)
        {
            query = query.Where(a => a.DueDate <= request.To);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(a => a.DueDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AccountPayableSummary(
                a.Id.Value, a.Description, a.Amount.Amount, a.Amount.Currency, a.DueDate, a.Category,
                a.Status, a.PaidAtUtc, a.ResourceId, a.SourceAppointmentId))
            .ToListAsync(cancellationToken);

        return Result.Success(new ListAccountsPayableResult(items, totalCount, page, pageSize));
    }
}
