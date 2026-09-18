namespace Agendio.Modules.Feedback.Contracts;

/// <summary>
/// Leitura sincrona do feedback livre enviado pelos usuarios (qualquer tenant)
/// para o painel do Super Admin (Platform) — o Platform nunca le a tabela
/// feedback_entries diretamente (regra de modulo em CLAUDE.md), so atraves
/// desta interface.
/// </summary>
public interface IFeedbackReader
{
    Task<IReadOnlyList<FeedbackEntrySummary>> GetRecentAsync(int maxEntries, CancellationToken cancellationToken = default);
}

public sealed record FeedbackEntrySummary(
    Guid Id,
    Guid TenantId,
    Guid SubmittedByUserId,
    string Subject,
    string Body,
    DateTimeOffset CreatedAtUtc);
