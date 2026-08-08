using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Marketing.Application.SendCampaign;

public sealed record SendCampaignCommand(string Subject, string Body) : ICommand<SendCampaignResult>;

public sealed record SendCampaignResult(Guid CampaignId, int RecipientCount);
