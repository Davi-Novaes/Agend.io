namespace Agendio.Infrastructure.Persistence;

/// <summary>
/// Trilha de auditoria append-only: quem alterou o que, quando, com o antes/depois
/// em JSON. Cada modulo tem a PROPRIA tabela (mesmo padrao de OutboxMessage) —
/// nunca compartilhada entre modulos, e nunca alterada depois de escrita (so
/// INSERT, nunca UPDATE/DELETE por codigo de aplicacao).
///
/// Simplificacao deliberada em relacao ao plano original: sem particionamento
/// mensal da tabela ainda — retencao/particionamento e uma preocupacao de escala
/// que vale a pena resolver quando o volume real exigir, nao antes.
/// </summary>
public sealed class AuditLogEntry
{
    public Guid Id { get; private init; }

    /// <summary>Null para entidades que nao sao ITenantOwned (nenhuma neste momento, mas o campo fica pronto).</summary>
    public Guid? TenantId { get; private init; }

    public string EntityType { get; private init; } = string.Empty;

    public Guid EntityId { get; private init; }

    public string Action { get; private init; } = string.Empty;

    public string? Before { get; private init; }

    public string? After { get; private init; }

    public string PerformedBy { get; private init; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private init; }

    private AuditLogEntry()
    {
    }

    public static AuditLogEntry Create(
        Guid? tenantId, string entityType, Guid entityId, string action, string? before, string? after, string performedBy, DateTimeOffset occurredAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Before = before,
            After = after,
            PerformedBy = performedBy,
            OccurredAtUtc = occurredAtUtc,
        };
}
