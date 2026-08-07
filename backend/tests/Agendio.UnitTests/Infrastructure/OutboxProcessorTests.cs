using Agendio.Infrastructure.Messaging;
using Agendio.Infrastructure.Persistence;
using Agendio.SharedKernel.DomainEvents;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agendio.UnitTests.Infrastructure;

/// <summary>
/// O criterio de pronto do Sprint 5: uma falha de rede simulada na publicacao
/// (RabbitMQ fora do ar) nao perde a mensagem — ela fica pendente com erro e e
/// reprocessada na proxima passada. Roda isolado com EF Core InMemory (nunca
/// usado em producao) em vez do Postgres real da suite de integracao, porque o
/// job recorrente de outbox de verdade (Hangfire, ativado no Sprint 5) tambem
/// fica escutando a mesma tabela real e ganharia a corrida contra o publisher
/// falho deste teste antes dele conseguir observar o estado intermediario.
/// </summary>
public class OutboxProcessorTests
{
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : AgendioDbContextBase(options);

    private sealed record TestDomainEvent(Guid EventId, DateTimeOffset OccurredOnUtc) : IDomainEvent;

    private sealed class FailFirstNCallsPublisher(int failCount) : IIntegrationEventPublisher
    {
        public int CallCount { get; private set; }

        public Task PublishRawAsync(string eventTypeName, string jsonContent, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return CallCount <= failCount
                ? Task.FromException(new InvalidOperationException("Falha de rede simulada: RabbitMQ inalcancavel."))
                : Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Message_Stays_Pending_On_Publish_Failure_And_Is_Reprocessed_On_The_Next_Run()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new TestDbContext(options);

        var domainEvent = new TestDomainEvent(Guid.NewGuid(), DateTimeOffset.UtcNow);
        dbContext.OutboxMessages.Add(OutboxMessage.FromDomainEvent(domainEvent));
        await dbContext.SaveChangesAsync(cancellationToken);

        var publisher = new FailFirstNCallsPublisher(failCount: 1);
        var processor = new OutboxProcessor<TestDbContext>(
            dbContext, publisher, new SystemClock(), NullLogger<OutboxProcessor<TestDbContext>>.Instance);

        // 1a passada: publisher simula a rede caida — a mensagem tem que
        // continuar pendente, com o erro registrado, sem lancar pra fora (uma
        // mensagem com falha nao pode travar o restante do lote).
        await processor.ProcessPendingMessagesAsync(cancellationToken);

        var afterFailedAttempt = await dbContext.OutboxMessages.SingleAsync(m => m.Id == domainEvent.EventId, cancellationToken);
        afterFailedAttempt.ProcessedOnUtc.ShouldBeNull();
        afterFailedAttempt.Error.ShouldNotBeNullOrWhiteSpace();

        // 2a passada: rede "voltou" — o mesmo processor reprocessa a MESMA
        // linha (nunca duplica, nunca perde) e agora marca como processada.
        await processor.ProcessPendingMessagesAsync(cancellationToken);

        var afterRetry = await dbContext.OutboxMessages.SingleAsync(m => m.Id == domainEvent.EventId, cancellationToken);
        afterRetry.ProcessedOnUtc.ShouldNotBeNull();
        publisher.CallCount.ShouldBe(2);
    }
}
