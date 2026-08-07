using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Billing.Application.ListPlans;

public sealed class ListPlansQueryHandler(BillingDbContext dbContext) : IQueryHandler<ListPlansQuery, IReadOnlyList<PlanSummary>>
{
    public async Task<Result<IReadOnlyList<PlanSummary>>> Handle(ListPlansQuery request, CancellationToken cancellationToken)
    {
        var plans = await dbContext.Plans
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.PriceAmount)
            .Select(p => new PlanSummary(p.Id.Value, p.Name, p.PriceAmount, p.Currency, p.BillingCycle.ToString()))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PlanSummary>>(plans);
    }
}
