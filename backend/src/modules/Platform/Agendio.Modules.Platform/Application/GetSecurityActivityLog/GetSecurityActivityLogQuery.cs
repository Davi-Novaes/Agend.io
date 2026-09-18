using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Platform.Application.GetSecurityActivityLog;

/// <summary>Sem parametros de proposito: janela e limite fixos (ver handler) — e um painel de monitoramento, nao um relatorio paginavel; filtro/busca acontece no frontend sobre a lista ja carregada.</summary>
public sealed record GetSecurityActivityLogQuery : IQuery<IReadOnlyList<SecurityActivityEntry>>;

public sealed record SecurityActivityEntry(
    Guid Id,
    // "Identity" (login/cadastro de usuario de tenant) ou "Platform" (login de Super Admin).
    string Source,
    string? TenantName,
    string EventType,
    bool Success,
    string? IpAddress,
    string? CountryCode,
    string? Region,
    string? City,
    string? UserAgent,
    string? Metadata,
    DateTimeOffset OccurredAtUtc);
