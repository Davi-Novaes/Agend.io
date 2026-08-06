using System.Text.Json;
using Agendio.SharedKernel.DomainEvents;

namespace Agendio.Infrastructure.Persistence;

/// <summary>
/// Registro do Transactional Outbox: grava o evento de dominio na MESMA transacao
/// que persiste o agregado. Um worker (Hangfire) le as linhas pendentes e publica
/// no RabbitMQ separadamente — assim "agendamento criado mas lembrete nunca
/// enviado" deixa de ser possivel: ou os dois sao salvos juntos, ou nenhum e.
///
/// Nao implementa ITenantOwned/nao tem RLS: e uma tabela de infraestrutura interna
/// (nunca exposta via endpoint de CRUD), nao uma entidade de negocio — a regra
/// "toda entidade ITenantOwned precisa de RLS" nao se aplica aqui por definicao.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private init; }

    /// <summary>
    /// Nome qualificado do tipo do evento. Preso ao assembly da versao atual —
    /// aceitavel enquanto o sistema e um monolito modular com deploy unico; ao
    /// migrar para microsservicos isso precisa virar um nome logico estavel
    /// (ex.: "identity.user-registered.v1") independente de assembly.
    /// </summary>
    public string Type { get; private init; } = string.Empty;

    public string Content { get; private init; } = string.Empty;

    public DateTimeOffset OccurredOnUtc { get; private init; }

    public DateTimeOffset? ProcessedOnUtc { get; private set; }

    public string? Error { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage FromDomainEvent(IDomainEvent domainEvent)
    {
        var eventType = domainEvent.GetType();

        return new OutboxMessage
        {
            Id = domainEvent.EventId,
            Type = eventType.AssemblyQualifiedName!,
            Content = JsonSerializer.Serialize(domainEvent, eventType),
            OccurredOnUtc = domainEvent.OccurredOnUtc,
        };
    }

    public void MarkProcessed(DateTimeOffset processedOnUtc) => ProcessedOnUtc = processedOnUtc;

    public void MarkFailed(string error) => Error = error;
}
