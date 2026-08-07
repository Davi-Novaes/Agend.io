using Agendio.Modules.Billing.Domain;
using Agendio.Modules.Billing.Infrastructure.Persistence;
using Agendio.Modules.Billing.Infrastructure.Persistence.Configurations;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Agendio.Modules.Billing.Infrastructure;

/// <summary>
/// Find-or-create de Subscription com retry em corrida de criacao — usado por
/// todo caminho que pode ser o primeiro a "tocar" a assinatura de um tenant
/// (GetMySubscriptionQueryHandler, SubscribeToPlanCommandHandler). Extraido
/// aqui porque os dois precisam exatamente da mesma logica de idempotencia
/// contra o indice unico de subscriptions.tenant_id — duplicar isso arriscaria
/// os dois divergirem sutilmente com o tempo.
/// </summary>
internal static class SubscriptionProvisioning
{
    public static async Task<Subscription> FindOrCreateAsync(
        BillingDbContext dbContext, TenantId tenantId, IClock clock, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Subscriptions.SingleOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var subscription = Subscription.StartTrial(tenantId, PlanConfiguration.DefaultPlanId, clock.UtcNow);
        dbContext.Subscriptions.Add(subscription);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return subscription;
        }
        catch (DbUpdateException)
        {
            // Corrida com o consumidor de evento ou outra requisicao concorrente
            // — o indice unico barrou a duplicata, so falta buscar a que ja existe.
            dbContext.Entry(subscription).State = EntityState.Detached;
            return await dbContext.Subscriptions.SingleAsync(s => s.TenantId == tenantId, cancellationToken);
        }
    }
}
