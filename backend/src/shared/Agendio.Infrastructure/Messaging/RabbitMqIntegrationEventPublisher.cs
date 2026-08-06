using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Agendio.Infrastructure.Messaging;

/// <summary>
/// Publisher via exchange topico duravel. Registrado como singleton: mantem UMA
/// conexao TCP viva e cria um canal por publicacao (IChannel nao e thread-safe
/// para uso concorrente — reabrir canal por chamada evita problema de concorrencia
/// sem precisar de pool de canais, aceitavel no volume do Sprint 0/1).
/// </summary>
public sealed class RabbitMqIntegrationEventPublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqIntegrationEventPublisher> logger) : IIntegrationEventPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public async Task PublishRawAsync(string eventTypeName, string jsonContent, CancellationToken cancellationToken = default)
    {
        var connection = await GetOrCreateConnectionAsync(cancellationToken);

        var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await using (channel)
        {
            await channel.ExchangeDeclareAsync(
                _options.ExchangeName,
                ExchangeType.Topic,
                durable: true,
                cancellationToken: cancellationToken);

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Type = eventTypeName,
                MessageId = Guid.NewGuid().ToString(),
            };

            await channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: eventTypeName,
                mandatory: false,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(jsonContent),
                cancellationToken: cancellationToken);
        }

        logger.LogDebug("Evento {EventType} publicado em {Exchange}", eventTypeName, _options.ExchangeName);
    }

    private async Task<IConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                AutomaticRecoveryEnabled = true,
                ClientProvidedName = "agendio-api",
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();
    }
}
