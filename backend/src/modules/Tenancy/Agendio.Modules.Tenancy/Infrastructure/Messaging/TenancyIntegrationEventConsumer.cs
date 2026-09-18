using System.Text;
using System.Text.Json;
using Agendio.Infrastructure.Messaging;
using Agendio.Modules.Identity.Contracts;
using Agendio.Modules.Tenancy.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Agendio.Modules.Tenancy.Infrastructure.Messaging;

/// <summary>
/// SEGUNDO consumidor de integration event do projeto (mesmo molde exato de
/// BillingIntegrationEventConsumer — ver o comentario la para o raciocinio
/// completo do bind "#"+filtro no codigo). Escuta UserRegistered do Identity
/// para saber quando um tenant deixa de ser "recem-criado, ainda sem
/// credencial" e vira "tem dono de verdade" — o unico sinal que
/// OrphanTenantCleanupJob usa pra distinguir os dois casos (P1-1,
/// docs/AUTH_BILLING_SECURITY_AUDIT.md).
/// </summary>
public sealed class TenancyIntegrationEventConsumer(
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<TenancyIntegrationEventConsumer> logger) : BackgroundService
{
    private const string QueueName = "tenancy.user-registered";
    private readonly RabbitMqOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            AutomaticRecoveryEnabled = true,
            ClientProvidedName = "agendio-tenancy-consumer",
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(QueueName, _options.ExchangeName, routingKey: "#", cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                await HandleMessageAsync(delivery, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao processar mensagem em {Queue} — descartada.", QueueName);
            }
            finally
            {
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        var eventType = delivery.BasicProperties.Type;
        if (string.IsNullOrEmpty(eventType) ||
            !eventType.StartsWith(IdentityIntegrationEventTypes.UserRegistered, StringComparison.Ordinal))
        {
            return;
        }

        var json = Encoding.UTF8.GetString(delivery.Body.Span);
        var payload = JsonSerializer.Deserialize<UserRegisteredPayload>(json);
        if (payload is null)
        {
            logger.LogWarning("Payload invalido para {EventType}: {Json}", eventType, json);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var tenantId = TenantId.From(payload.TenantId.Value);

        // Idempotente por construcao: MarkFirstUserRegistered so seta na
        // primeira chamada (??=) — redelivery do mesmo evento nao muda nada.
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return;
        }

        tenant.MarkFirstUserRegistered(clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record TenantIdPayload(Guid Value);

    private sealed record UserIdPayload(Guid Value);

    private sealed record UserRegisteredPayload(UserIdPayload UserId, TenantIdPayload TenantId, string Email);
}
