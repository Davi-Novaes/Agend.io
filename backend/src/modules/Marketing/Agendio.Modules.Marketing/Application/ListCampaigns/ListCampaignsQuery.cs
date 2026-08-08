using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Marketing.Application.ListCampaigns;

public sealed record ListCampaignsQuery(int Page = 1, int PageSize = 20) : IQuery<ListCampaignsResult>;

public sealed record CampaignSummary(Guid Id, string Subject, int RecipientCount, DateTimeOffset SentAtUtc);

public sealed record ListCampaignsResult(IReadOnlyList<CampaignSummary> Items, int TotalCount, int Page, int PageSize);
