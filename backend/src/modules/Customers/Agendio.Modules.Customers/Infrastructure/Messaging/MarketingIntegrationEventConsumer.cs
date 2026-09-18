using System.Text;
using System.Text.Json;
using Agendio.Infrastructure.Messaging;
using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.Modules.Marketing.Contracts;
using Agendio.SharedKernel.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Agendio.Modules.Customers.Infrastructure.Messaging;

/// <summary>
/// Mesmo molde de LoyaltyIntegrationEventConsumer (ver comentario la). Escuta
/// CampaignSent do Marketing pra marcar LastContactedAtUtc de cada
/// destinatario — pedido explicito do usuario (2026-09-05): o card "Clientes
/// para recuperar" (GetCustomerRecoveryCandidatesQueryHandler, Scheduling) nao
/// deve continuar sugerindo quem acabou de receber uma campanha.
/// </summary>
public sealed class MarketingIntegrationEventConsumer(
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<MarketingIntegrationEventConsumer> logger) : BackgroundService
{
    private const string QueueName = "customers.campaign-sent.mark-contacted";
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
            ClientProvidedName = "agendio-customers-marketing-consumer",
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
            !eventType.StartsWith(MarketingIntegrationEventTypes.CampaignSent, StringComparison.Ordinal))
        {
            return;
        }

        var json = Encoding.UTF8.GetString(delivery.Body.Span);
        var payload = JsonSerializer.Deserialize<CampaignSentPayload>(json);
        if (payload is null || payload.CustomerIds.Count == 0)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        // Ancora o tenant ANTES de qualquer acesso ao CustomersDbContext — mesmo
        // motivo do LoyaltyIntegrationEventConsumer: sem isso o RLS rejeita a query.
        tenantContext.SetTenant(TenantId.From(payload.TenantId.Value));

        var ids = payload.CustomerIds.Select(CustomerId.From).ToList();
        var customers = await dbContext.Customers
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(cancellationToken);

        foreach (var customer in customers)
        {
            customer.MarkContacted(payload.SentAtUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record TenantIdPayload(Guid Value);

    private sealed record CampaignSentPayload(TenantIdPayload TenantId, List<Guid> CustomerIds, DateTimeOffset SentAtUtc);
}
