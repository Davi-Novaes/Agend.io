namespace Agendio.Modules.Identity.Contracts;

/// <summary>
/// Leitura sincrona da trilha de auditoria de seguranca (login/registro de
/// tenant users) para o painel do Super Admin (Platform) — o Platform nunca
/// le a tabela security_audit_log de Identity diretamente (regra de modulo em
/// CLAUDE.md), so atraves desta interface.
/// </summary>
public interface ISecurityAuditReader
{
    Task<IReadOnlyList<SecurityAuditEvent>> GetRecentEventsAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken = default);
}

public sealed record SecurityAuditEvent(
    Guid Id,
    Guid? TenantId,
    Guid? ActorId,
    string EventType,
    bool Success,
    string? IpAddress,
    string? CountryCode,
    string? Region,
    string? City,
    string? UserAgent,
    string? Metadata,
    DateTimeOffset OccurredAtUtc);
