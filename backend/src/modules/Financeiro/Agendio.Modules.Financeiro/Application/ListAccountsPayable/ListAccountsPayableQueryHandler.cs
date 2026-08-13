using Agendio.Infrastructure.Persistence;
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

        var paged = await query
            .OrderBy(a => a.DueDate)
            .Select(a => new AccountPayableSummary(
                a.Id.Value, a.Description, a.Amount.Amount, a.Amount.Currency, a.DueDate, a.Category,
                a.Status, a.PaidAtUtc, a.ResourceId, a.SourceAppointmentId))
            .ToPagedItemsAsync(request.Page, request.PageSize, cancellationToken);

        return Result.Success(new ListAccountsPayableResult(paged.Items, paged.TotalCount, paged.Page, paged.PageSize));
    }
}
