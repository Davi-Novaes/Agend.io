namespace Agendio.Modules.Assistant.Infrastructure;

/// <summary>
/// Grava um AssistantQueryLogEntry (ver Domain) de forma best-effort -- a
/// implementacao NUNCA deixa uma falha de log derrubar a resposta ao usuario
/// (log de auditoria e observacao secundaria, nao pode ser single point of
/// failure da feature principal).
/// </summary>
public interface IAssistantQueryLogger
{
    Task LogAsync(
        Guid userId,
        string question,
        string? resolvedIntent,
        string source,
        string? toolOrServiceUsed,
        bool success,
        string? errorCode,
        CancellationToken cancellationToken);
}
