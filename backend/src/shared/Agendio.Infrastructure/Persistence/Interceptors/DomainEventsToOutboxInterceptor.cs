using Agendio.SharedKernel.DomainEvents;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Agendio.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Antes de cada SaveChanges, varre o ChangeTracker por agregados com eventos de
/// dominio pendentes e grava cada um na tabela outbox — na MESMA transacao do
/// SaveChanges, porque este codigo roda dentro do mesmo ciclo de SavingChanges.
/// </summary>
public sealed class DomainEventsToOutboxInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AppendDomainEventsToOutbox(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AppendDomainEventsToOutbox(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private static void AppendDomainEventsToOutbox(Microsoft.EntityFrameworkCore.DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var aggregatesWithEvents = context.ChangeTracker.Entries()
            .Select(entry => entry.Entity)
            .OfType<IHasDomainEvents>()
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregatesWithEvents)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                context.Set<OutboxMessage>().Add(OutboxMessage.FromDomainEvent(domainEvent));
            }

            aggregate.ClearDomainEvents();
        }
    }
}
