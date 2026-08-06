namespace Agendio.Infrastructure.Messaging;

/// <summary>
/// Publica eventos ja serializados (o OutboxProcessor le a tabela outbox e chama
/// isto — nao republica o dominio inteiro, so o payload que ja foi persistido).
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishRawAsync(string eventTypeName, string jsonContent, CancellationToken cancellationToken = default);
}
