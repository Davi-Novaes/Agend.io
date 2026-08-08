using System.Text;
using System.Text.Json;
using Agendio.Infrastructure.Messaging;
using Agendio.Modules.Financeiro.Domain;
using Agendio.Modules.Financeiro.Infrastructure.Persistence;
using Agendio.Modules.Scheduling.Contracts;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Time;
using Agendio.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Agendio.Modules.Financeiro.Infrastructure.Messaging;

/// <summary>
/// SEGUNDO consumidor de integration event do projeto (o primeiro foi
/// BillingIntegrationEventConsumer, Sprint 7 — mesmo desenho, clonado
/// deliberadamente em vez de generalizado, so existem dois hoje). Ao concluir
/// um agendamento: cria a conta a receber (Pending) e, se o profissional tiver
/// uma CommissionRule ativa, a conta a pagar da comissao junto.
///
/// Diferenca importante em relacao ao Billing: Subscription/Payment NAO sao
/// ITenantOwned (sem RLS, ver plano do Sprint 7), mas AccountReceivable/
/// AccountPayable SAO — entao aqui, ao contrario do consumidor do Billing,
/// e OBRIGATORIO ancorar tenantContext.SetTenant(...) antes de tocar o
/// FinanceiroDbContext, ou o TenantConnectionInterceptor manda SET
/// app.tenant_id pro sentinela vazio e a politica de RLS rejeita o INSERT.
/// </summary>
public sealed class FinancialIntegrationEventConsumer(
    IOptions<RabbitMqOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<FinancialIntegrationEventConsumer> logger) : BackgroundService
{
    private const string QueueName = "financeiro.appointment-completed";
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
            ClientProvidedName = "agendio-financeiro-consumer",
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
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceiroDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var tenantId = TenantId.From(payload.TenantId.Value);
        var appointmentId = payload.AppointmentId.Value;

        // Ancora o tenant ANTES de qualquer acesso ao FinanceiroDbContext — ver
        // doc comment da classe: sem isso o RLS rejeita o INSERT.
        tenantContext.SetTenant(tenantId);

        // Idempotente: redelivery do mesmo evento nao duplica a receita — o
        // indice unico parcial em SourceAppointmentId e a rede de seguranca final.
        var alreadyExists = await dbContext.AccountsReceivable.AnyAsync(a => a.SourceAppointmentId == appointmentId, cancellationToken);
        if (alreadyExists)
        {
            return;
        }

        var price = Money.Create(payload.Price.Amount, payload.Price.Currency).Value;
        var completedDate = DateOnly.FromDateTime(payload.CompletedAtUtc.UtcDateTime);

        var receivableResult = AccountReceivable.Create(tenantId, payload.ServiceName, price, completedDate, appointmentId);
        if (receivableResult.IsFailure)
        {
            logger.LogWarning(
                "Nao foi possivel criar AccountReceivable para o agendamento {AppointmentId}: {Error}",
                appointmentId, receivableResult.Error.Message);
            return;
        }

        dbContext.AccountsReceivable.Add(receivableResult.Value);

        var commissionRule = await dbContext.CommissionRules
            .SingleOrDefaultAsync(c => c.ResourceId == payload.ResourceId && c.IsActive, cancellationToken);

        if (commissionRule is not null)
        {
            var commissionAmount = commissionRule.CalculateCommission(price);

            // Comissao zero (regra existe mas calculou R$0,00 — ex.: valor fixo
            // zerado) nao gera conta a pagar nenhuma, evita ruido no fluxo de caixa.
            if (commissionAmount.Amount > 0)
            {
                var payableResult = AccountPayable.Create(
                    tenantId, $"Comissao — {payload.ServiceName}", commissionAmount, completedDate,
                    ExpenseCategory.Commission, payload.ResourceId, appointmentId);

                if (payableResult.IsSuccess)
                {
                    dbContext.AccountsPayable.Add(payableResult.Value);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Conta a receber criada para o agendamento {AppointmentId} concluido.", appointmentId);
    }

    private sealed record AppointmentIdPayload(Guid Value);

    private sealed record TenantIdPayload(Guid Value);

    private sealed record MoneyPayload(decimal Amount, string Currency);

    private sealed record AppointmentCompletedPayload(
        AppointmentIdPayload AppointmentId, TenantIdPayload TenantId, Guid ResourceId, string ServiceName,
        MoneyPayload Price, DateTimeOffset CompletedAtUtc);
}
