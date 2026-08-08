using Agendio.Modules.Marketing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Marketing.Application.ListCampaigns;

public sealed class ListCampaignsQueryHandler(MarketingDbContext dbContext) : IQueryHandler<ListCampaignsQuery, ListCampaignsResult>
{
    public async Task<Result<ListCampaignsResult>> Handle(ListCampaignsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = dbContext.Campaigns.AsNoTracking().AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.SentAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CampaignSummary(c.Id.Value, c.Subject, c.RecipientCount, c.SentAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(new ListCampaignsResult(items, totalCount, page, pageSize));
    }
}
