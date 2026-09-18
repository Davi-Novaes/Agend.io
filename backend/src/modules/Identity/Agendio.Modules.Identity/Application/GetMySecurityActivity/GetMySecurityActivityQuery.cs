using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Identity.Application.GetMySecurityActivity;

/// <summary>UserId vem da claim do JWT — a tela de Configuracoes/Seguranca usa isto pra listar so os eventos do proprio usuario (nunca de outro tenant/conta).</summary>
public sealed record GetMySecurityActivityQuery(Guid UserId) : IQuery<IReadOnlyList<MySecurityActivityEntry>>;

public sealed record MySecurityActivityEntry(
    Guid Id,
    string EventType,
    bool Success,
    string? IpAddress,
    string? CountryCode,
    string? Region,
    string? City,
    string? UserAgent,
    DateTimeOffset OccurredAtUtc);
