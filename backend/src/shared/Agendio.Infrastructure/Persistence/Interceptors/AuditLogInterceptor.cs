using System.Text.Json;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Time;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Agendio.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Grava uma linha em audit_log para toda entidade ITenantOwned criada, alterada
/// ou removida — o "quem/o que/quando/antes/depois" exigido pelo CLAUDE.md.
/// Roda DEPOIS de AuditingSaveChangesInterceptor na cadeia (ver
/// PersistenceServiceCollectionExtensions.AddModuleDbContext): aquele interceptor
/// ja converteu delete fisico em soft-delete quando aplicavel, entao aqui uma
/// entidade ISoftDeletable marcada como excluida chega como State.Modified, nao
/// State.Deleted — e registrada como "Updated" com IsDeleted=true no After, o que
/// e perfeitamente auditavel (mostra exatamente o campo que mudou) ainda que o
/// rotulo da acao nao seja literalmente "Deleted".
///
/// So captura entidades ITenantOwned de proposito: exclui tabelas de
/// infraestrutura (OutboxMessage, AuditLogEntry) sem precisar de uma lista de
/// exclusao explicita.
/// </summary>
public sealed class AuditLogInterceptor(IClock clock, IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    // Nunca gravar estes dados no log de auditoria, mesmo que uma entidade
    // ITenantOwned futura os exponha como propriedade mapeada — regra
    // inegociavel do CLAUDE.md (senha, token, CPF, dado de saude).
    private static readonly string[] SensitiveNameFragments =
        ["password", "hash", "token", "secret", "cpf", "healthnotes"];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CaptureAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void CaptureAuditEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var performedBy = httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? "system";
        var now = clock.UtcNow;

        var entries = new List<AuditLogEntry>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not ITenantOwned tenantOwned)
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted => "Deleted",
                _ => null,
            };

            if (action is null)
            {
                continue;
            }

            // Nao dá pra ler o Id convertido via PropertyEntry.CurrentValue: o
            // ValueConverter (TypedId <-> Guid) so atua no lado do provider, e
            // CurrentValue devolve o valor no "espaco do modelo" — ou seja, a
            // propria instancia de TypedId (CustomerId, AppointmentId...), nao
            // um Guid. Toda entidade de dominio segue a convencao Entity<TId>.Id
            // com TId : TypedId (ver Agendio.SharedKernel.Primitives), entao
            // reflection aqui e mais simples e generico do que exigir uma
            // interface extra soh para o log de auditoria.
            if (entry.Entity.GetType().GetProperty("Id")?.GetValue(entry.Entity) is not TypedId typedId)
            {
                continue;
            }

            var entityIdGuid = typedId.Value;

            var before = action == "Created" ? null : SerializeValues(entry.OriginalValues);
            var after = action == "Deleted" ? null : SerializeValues(entry.CurrentValues);

            entries.Add(AuditLogEntry.Create(
                tenantOwned.TenantId.Value,
                entry.Entity.GetType().Name,
                entityIdGuid,
                action,
                before,
                after,
                performedBy,
                now));
        }

        if (entries.Count > 0)
        {
            context.Set<AuditLogEntry>().AddRange(entries);
        }
    }

    private static string SerializeValues(PropertyValues values)
    {
        var snapshot = values.Properties.ToDictionary(
            property => property.Name,
            property => IsSensitive(property.Name) ? "***" : values[property]);

        return JsonSerializer.Serialize(snapshot);
    }

    private static bool IsSensitive(string propertyName) =>
        SensitiveNameFragments.Any(fragment => propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
