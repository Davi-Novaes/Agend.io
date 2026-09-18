namespace Agendio.Modules.Identity.Contracts;

/// <summary>
/// Nome estavel dos integration events que Identity publica no RabbitMQ — mesmo
/// papel de Tenancy.Contracts.TenancyIntegrationEventTypes, ver o comentario la
/// para o raciocinio completo do prefixo/routing key.
/// </summary>
public static class IdentityIntegrationEventTypes
{
    public const string UserRegistered = "Agendio.Modules.Identity.Domain.UserRegisteredDomainEvent";
}
