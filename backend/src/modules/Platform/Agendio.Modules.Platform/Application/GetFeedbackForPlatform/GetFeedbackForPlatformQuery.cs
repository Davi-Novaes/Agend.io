using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Platform.Application.GetFeedbackForPlatform;

/// <summary>Sem parametros de proposito -- mesmo raciocinio de GetSecurityActivityLogQuery: painel de monitoramento, nao relatorio paginavel.</summary>
public sealed record GetFeedbackForPlatformQuery : IQuery<IReadOnlyList<PlatformFeedbackEntry>>;

public sealed record PlatformFeedbackEntry(
    Guid Id,
    string? TenantName,
    string Subject,
    string Body,
    DateTimeOffset CreatedAtUtc);
