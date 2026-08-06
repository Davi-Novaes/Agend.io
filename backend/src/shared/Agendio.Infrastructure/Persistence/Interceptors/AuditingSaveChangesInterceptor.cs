using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Agendio.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Preenche IAuditable/ISoftDeletable automaticamente no SaveChanges. Nenhum
/// codigo de dominio ou de aplicacao escreve CreatedAtUtc/UpdatedBy diretamente —
/// isso e responsabilidade de infraestrutura, nao de regra de negocio.
/// Tambem converte delete fisico em soft-delete quando a entidade suporta.
/// </summary>
public sealed class AuditingSaveChangesInterceptor(IClock clock, ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAuditing(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditing(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditing(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = clock.UtcNow;
        var actor = tenantContext.HasTenant ? tenantContext.TenantSlug : "system";

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable softDeletable })
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAtUtc = now;
                continue;
            }

            if (entry.Entity is not IAuditable auditable)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.CreatedAtUtc = now;
                    auditable.CreatedBy = actor;
                    break;
                case EntityState.Modified:
                    auditable.UpdatedAtUtc = now;
                    auditable.UpdatedBy = actor;
                    break;
            }
        }
    }
}
