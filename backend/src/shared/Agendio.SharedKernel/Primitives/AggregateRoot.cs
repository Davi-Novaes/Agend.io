using Agendio.SharedKernel.DomainEvents;

namespace Agendio.SharedKernel.Primitives;

/// <summary>
/// Raiz de agregado: unico ponto de entrada para mudancas dentro do agregado, e
/// unico lugar que produz eventos de dominio. Os eventos ficam na tabela outbox
/// na MESMA transacao que persiste o agregado (ver Agendio.Infrastructure) — assim
/// nunca existe um evento "orfao" de uma mudanca que nao foi salva.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
