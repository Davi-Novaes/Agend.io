namespace Agendio.Modules.Tenancy.Contracts;

/// <summary>
/// Nome estavel (sem sufixo de assembly/versao) dos integration events que
/// Tenancy publica no RabbitMQ, para outros modulos consumirem sem referenciar
/// Tenancy.Domain diretamente (proibido pela regra de dependencia entre
/// modulos). O routing key/BasicProperties.Type real de cada mensagem e o
/// AssemblyQualifiedName completo (ver OutboxMessage.FromDomainEvent) — quem
/// consome compara o prefixo antes da primeira virgula contra esta constante.
/// </summary>
public static class TenancyIntegrationEventTypes
{
    public const string TenantCreated = "Agendio.Modules.Tenancy.Domain.TenantCreatedDomainEvent";
}
