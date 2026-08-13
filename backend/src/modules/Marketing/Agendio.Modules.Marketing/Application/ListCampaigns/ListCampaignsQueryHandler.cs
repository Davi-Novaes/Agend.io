using Agendio.Infrastructure.Persistence;
using Agendio.Modules.Marketing.Infrastructure.Persistence;
using Agendio.SharedKernel.Messaging;
using Agendio.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Marketing.Application.ListCampaigns;

public sealed class ListCampaignsQueryHandler(MarketingDbContext dbContext) : IQueryHandler<ListCampaignsQuery, ListCampaignsResult>
{
    public async Task<Result<ListCampaignsResult>> Handle(ListCampaignsQuery request, CancellationToken cancellationToken)
    {
        var paged = await dbContext.Campaigns.AsNoTracking()
            .OrderByDescending(c => c.SentAtUtc)
            .Select(c => new CampaignSummary(c.Id.Value, c.Subject, c.RecipientCount, c.SentAtUtc))
            .ToPagedItemsAsync(request.Page, request.PageSize, cancellationToken);

        return Result.Success(new ListCampaignsResult(paged.Items, paged.TotalCount, paged.Page, paged.PageSize));
    }
}
