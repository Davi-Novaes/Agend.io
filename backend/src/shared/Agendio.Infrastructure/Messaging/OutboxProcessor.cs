using Agendio.SharedKernel.Time;
using Agendio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Agendio.Infrastructure.Messaging;

/// <summary>
/// Le mensagens pendentes da tabela outbox do DbContext do modulo e publica no
/// RabbitMQ. Cada modulo registra isto como um Hangfire recurring job apontando
/// para o PROPRIO DbContext — genérico por TDbContext para nao acoplar
/// Infrastructure a nenhum modulo especifico.
/// </summary>
public sealed class OutboxProcessor<TDbContext>(
    TDbContext dbContext,
    IIntegrationEventPublisher publisher,
    IClock clock,
    ILogger<OutboxProcessor<TDbContext>> logger)
    where TDbContext : AgendioDbContextBase
{
    private const int BatchSize = 50;

    public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        var pendingMessages = await dbContext.OutboxMessages
            .Where(message => message.ProcessedOnUtc == null)
            .OrderBy(message => message.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return;
        }

        foreach (var message in pendingMessages)
        {
            try
            {
                await publisher.PublishRawAsync(message.Type, message.Content, cancellationToken);
                message.MarkProcessed(clock.UtcNow);
            }
            catch (Exception ex)
            {
                // Nao propaga: uma mensagem com falha nao pode travar as demais do lote.
                // Fica marcada com erro e sera reprocessada na proxima execucao do job
                // (ProcessedOnUtc continua null).
                logger.LogError(ex, "Falha ao publicar mensagem de outbox {MessageId} ({MessageType})", message.Id, message.Type);
                message.MarkFailed(ex.Message);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
