namespace Agendio.SharedKernel.DomainEvents;

/// <summary>
/// Contrato nao-generico de AggregateRoot&lt;TId&gt;. Existe para o interceptor de
/// outbox (Agendio.Infrastructure) conseguir varrer o ChangeTracker do EF Core com
/// <c>OfType&lt;IHasDomainEvents&gt;()</c> sem se importar com o tipo do Id de cada
/// agregado — o ChangeTracker devolve entidades como object, e AggregateRoot&lt;Guid&gt;
/// e AggregateRoot&lt;int&gt;, por exemplo, nao tem um ancestral comum generico util.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
