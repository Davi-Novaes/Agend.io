using Agendio.Modules.Assistant.Domain;
using Agendio.Modules.Assistant.Infrastructure.Persistence;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Time;
using Microsoft.Extensions.Logging;

namespace Agendio.Modules.Assistant.Infrastructure;

public sealed class AssistantQueryLogger(
    AssistantDbContext dbContext, ITenantContext tenantContext, IClock clock, ILogger<AssistantQueryLogger> logger)
    : IAssistantQueryLogger
{
    public async Task LogAsync(
        Guid userId,
        string question,
        string? resolvedIntent,
        string source,
        string? toolOrServiceUsed,
        bool success,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = AssistantQueryLogEntry.Create(
                tenantContext.TenantId, userId, question, resolvedIntent, source, toolOrServiceUsed, success, errorCode, clock.UtcNow);

            dbContext.AssistantQueryLogEntries.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Best-effort: log de auditoria nunca pode derrubar a resposta ao
            // usuario -- so registra o proprio erro de gravacao e segue.
            logger.LogWarning(ex, "Falha ao gravar log de auditoria do assistente");
        }
    }
}
