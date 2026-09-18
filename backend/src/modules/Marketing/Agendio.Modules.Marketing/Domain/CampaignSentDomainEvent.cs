using Agendio.SharedKernel.DomainEvents;
using Agendio.SharedKernel.Multitenancy;

namespace Agendio.Modules.Marketing.Domain;

/// <summary>
/// Consumido por Customers (MarketingIntegrationEventConsumer) pra marcar
/// LastContactedAtUtc de cada destinatario — assim GetCustomerRecoveryCandidates
/// (Scheduling) para de sugerir "recuperar" quem acabou de receber uma campanha.
/// </summary>
public sealed record CampaignSentDomainEvent(
    CampaignId CampaignId, TenantId TenantId, IReadOnlyList<Guid> CustomerIds, DateTimeOffset SentAtUtc) : DomainEvent;
