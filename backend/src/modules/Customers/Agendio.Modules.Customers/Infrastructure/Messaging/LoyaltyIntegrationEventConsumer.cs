using System.Text;
using System.Text.Json;
using Agendio.Infrastructure.Messaging;
using Agendio.Modules.Customers.Domain;
using Agendio.Modules.Customers.Infrastructure.Persistence;
using Agendio.Modules.Scheduling.Contracts;
using Agendio.Modules.Tenancy.Contracts;
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
/// Credita 1 ponto de fidelidade por visita concluida — mesmo desenho de
/// FinancialIntegrationEventConsumer (Financeiro), clonado deliberadamente
/// em vez de generalizado (ver doc comment da classe irma). Ao contrario do
/// Financeiro, aqui a credito e condicional: so acontece se o tenant tiver o
/// programa de fidelidade ligado (ITenantLookupService.GetLoyaltySettingsAsync)
/// — tenant que nunca configurou nada continua com o default sensato (ligado).
/// </summary>
public sealed class LoyaltyIntegrationEventConsumer(
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<LoyaltyIntegrationEventConsumer> logger) : BackgroundService
{
    private const string QueueName = "customers.appointment-completed.loyalty";
    private const int PointsPerVisit = 1;
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
            ClientProvidedName = "agendio-customers-loyalty-consumer",
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
            !eventType.StartsWith(SchedulingIntegrationEventTypes.AppointmentCompleted, StringComparison.Ordinal))
        {
            return;
        }

        var json = Encoding.UTF8.GetString(delivery.Body.Span);
        var payload = JsonSerializer.Deserialize<AppointmentCompletedPayload>(json);
        if (payload is null)
        {
            logger.LogWarning("Payload invalido para {EventType}: {Json}", eventType, json);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var tenantLookup = scope.ServiceProvider.GetRequiredService<ITenantLookupService>();

        var tenantId = TenantId.From(payload.TenantId.Value);
        var appointmentId = payload.AppointmentId.Value;

        // Ancora o tenant ANTES de qualquer acesso ao CustomersDbContext — mesmo
        // motivo do FinancialIntegrationEventConsumer: sem isso o RLS rejeita o INSERT.
        tenantContext.SetTenant(tenantId);

        var loyaltySettings = await tenantLookup.GetLoyaltySettingsAsync(tenantId, cancellationToken);
        if (loyaltySettings is null || !loyaltySettings.LoyaltyProgramEnabled)
        {
            return;
        }

        // Idempotente: redelivery do mesmo evento nao credita pontos duas
        // vezes — o indice unico parcial (tenant_id, appointment_id) WHERE
        // kind = 'Earned' e a rede de seguranca final.
        var alreadyExists = await dbContext.LoyaltyPointsLedgerEntries.AnyAsync(e => e.AppointmentId == appointmentId, cancellationToken);
        if (alreadyExists)
        {
            return;
        }

        var customer = await dbContext.Customers.SingleOrDefaultAsync(c => c.Id == CustomerId.From(payload.CustomerId), cancellationToken);
        if (customer is null)
        {
            logger.LogWarning("Cliente {CustomerId} nao encontrado ao creditar pontos de fidelidade do agendamento {AppointmentId}.", payload.CustomerId, appointmentId);
            return;
        }

        var earnResult = customer.EarnLoyaltyPoints(PointsPerVisit);
        if (earnResult.IsFailure)
        {
            logger.LogWarning("Nao foi possivel creditar pontos para o cliente {CustomerId}: {Error}", payload.CustomerId, earnResult.Error.Message);
            return;
        }

        var ledgerEntryResult = LoyaltyPointsLedgerEntry.RecordEarned(tenantId, customer.Id, PointsPerVisit, appointmentId, payload.CompletedAtUtc);
        if (ledgerEntryResult.IsFailure)
        {
            logger.LogWarning("Nao foi possivel registrar o lancamento de pontos do agendamento {AppointmentId}: {Error}", appointmentId, ledgerEntryResult.Error.Message);
            return;
        }

        dbContext.LoyaltyPointsLedgerEntries.Add(ledgerEntryResult.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Cliente {CustomerId} ganhou {Points} ponto(s) de fidelidade pelo agendamento {AppointmentId}.", payload.CustomerId, PointsPerVisit, appointmentId);
    }

    private sealed record AppointmentIdPayload(Guid Value);

    private sealed record TenantIdPayload(Guid Value);

    private sealed record AppointmentCompletedPayload(
        AppointmentIdPayload AppointmentId, TenantIdPayload TenantId, Guid CustomerId, DateTimeOffset CompletedAtUtc);
}
