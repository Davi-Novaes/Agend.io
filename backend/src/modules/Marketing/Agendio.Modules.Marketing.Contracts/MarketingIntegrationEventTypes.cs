namespace Agendio.Modules.Marketing.Contracts;

/// <summary>
/// Nome estavel dos integration events que Marketing publica no RabbitMQ — mesmo
/// papel de Identity.Contracts.IdentityIntegrationEventTypes, ver o comentario la
/// para o raciocinio completo do prefixo/routing key.
/// </summary>
public static class MarketingIntegrationEventTypes
{
    public const string CampaignSent = "Agendio.Modules.Marketing.Domain.CampaignSentDomainEvent";
}
