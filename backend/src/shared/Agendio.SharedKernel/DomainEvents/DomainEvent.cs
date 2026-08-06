namespace Agendio.SharedKernel.DomainEvents;

/// <summary>
/// Base para eventos de dominio. Excecao deliberada a regra "nunca DateTimeOffset.UtcNow
/// direto, sempre IClock": o evento e criado dentro do proprio agregado (POCO, sem
/// injecao de dependencia), e OccurredOnUtc aqui e metadado de infraestrutura do
/// evento (quando foi gerado em memoria), nao uma data de negocio — a data que
/// importa para regra de negocio (ex.: "esta dentro do prazo de cancelamento?")
/// sempre vem de IClock, passada explicitamente para o metodo do agregado.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredOnUtc { get; } = DateTimeOffset.UtcNow;
}
