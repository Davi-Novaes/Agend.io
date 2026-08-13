using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Financeiro.Application.ListAccountsReceivable;

public sealed class ListAccountsReceivableQueryHandler(FinanceiroDbContext dbContext)
    : IQueryHandler<ListAccountsReceivableQuery, ListAccountsReceivableResult>
{
    public async Task<Result<ListAccountsReceivableResult>> Handle(ListAccountsReceivableQuery request, CancellationToken cancellationToken)
    {
        // O Global Query Filter ja restringe isto ao tenant do JWT do chamador.
        var query = dbContext.AccountsReceivable.AsNoTracking().AsQueryable();

        if (request.Status is not null)
        {
            query = query.Where(a => a.Status == request.Status);
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
            .Select(a => new AccountReceivableSummary(
                a.Id.Value, a.Description, a.Amount.Amount, a.Amount.Currency, a.DueDate, a.Status, a.ReceivedAtUtc, a.SourceAppointmentId))
            .ToPagedItemsAsync(request.Page, request.PageSize, cancellationToken);

        return Result.Success(new ListAccountsReceivableResult(paged.Items, paged.TotalCount, paged.Page, paged.PageSize));
    }
}
