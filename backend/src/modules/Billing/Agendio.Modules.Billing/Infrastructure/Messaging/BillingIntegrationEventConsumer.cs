using System.Text;
using System.Text.Json;
using Agendio.Infrastructure.Messaging;
using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.Modules.Billing.Infrastructure.Persistence.Configurations;
using Agendio.Modules.Tenancy.Contracts;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Agendio.Modules.Billing.Infrastructure.Messaging;

/// <summary>
/// PRIMEIRO consumidor de integration event do projeto — ate aqui,
/// RabbitMqIntegrationEventPublisher so tinha publicadores, nunca um
/// assinante do outro lado. Existe para garantir que o trial de 14 dias
/// comeca EXATAMENTE na criacao do tenant, mesmo para o dono que nunca abre
/// a tela de cobranca — sem isso, so uma visita manual criaria a Subscription.
///
/// Mantido pequeno e especifico de proposito (sem infraestrutura generica de
/// "handler plugavel"): so existe UM consumidor e UM tipo de evento tratado
/// hoje, generalizar agora seria abstracao prematura.
///
/// Fila com bind "#" (recebe tudo da exchange), filtrando no codigo: o routing
/// key real de cada mensagem e o AssemblyQualifiedName inteiro (ver
/// OutboxMessage.FromDomainEvent), cheio de virgula e numero de versao — nao
/// da pra fazer bind exato sem quebrar a cada bump de versao do assembly.
/// </summary>
public sealed class BillingIntegrationEventConsumer(
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<BillingIntegrationEventConsumer> logger) : BackgroundService
{
    private const string QueueName = "billing.tenant-created";
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
            ClientProvidedName = "agendio-billing-consumer",
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
                // Loga e segue — nunca entra em loop de redelivery infinito.
                // Mesma filosofia do OutboxProcessor: uma mensagem com problema
                // nao pode travar o resto da fila.
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
            !eventType.StartsWith(TenancyIntegrationEventTypes.TenantCreated, StringComparison.Ordinal))
        {
            return;
        }

        var json = Encoding.UTF8.GetString(delivery.Body.Span);
        var payload = JsonSerializer.Deserialize<TenantCreatedPayload>(json);
        if (payload is null)
        {
            logger.LogWarning("Payload invalido para {EventType}: {Json}", eventType, json);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var tenantId = TenantId.From(payload.TenantId.Value);

        // Idempotente: redelivery do mesmo evento (ou uma corrida com o
        // auto-heal de GetMySubscriptionQueryHandler) nao duplica a linha —
        // o indice unico em subscriptions.tenant_id e a rede de seguranca final.
        var alreadyExists = await dbContext.Subscriptions.AnyAsync(s => s.TenantId == tenantId, cancellationToken);
        if (alreadyExists)
        {
            return;
        }

        var subscription = Subscription.StartTrial(tenantId, PlanConfiguration.DefaultPlanId, clock.UtcNow);
        dbContext.Subscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Trial de 14 dias iniciado para o tenant {TenantId}.", tenantId.Value);
    }

    private sealed record TenantIdPayload(Guid Value);

    private sealed record TenantCreatedPayload(TenantIdPayload TenantId, string Name, string Slug);
}
