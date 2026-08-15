using Agendio.Modules.Customers.Contracts;
using Agendio.Modules.Marketing.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Marketing.Application.SendCampaign;

// TargetSegment null = todos os clientes ativos elegiveis para o canal.
public sealed record SendCampaignCommand(string Subject, string Body, CampaignChannel Channel, CustomerSegment? TargetSegment)
    : ICommand<SendCampaignResult>;

public sealed record SendCampaignResult(Guid CampaignId, int RecipientCount);
