using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;

namespace Agendio.Modules.Assistant.Domain;

/// <summary>
/// Registro de auditoria de UMA pergunta feita ao assistente -- append-only,
/// nunca editado depois de criado (por isso nao implementa IAuditable: nao ha
/// "quem atualizou", so "quem perguntou e quando"). Guarda so o necessario pra
/// entender o uso e depurar erro, nunca dado sensivel:
/// - QuestionExcerpt e truncado (nunca a pergunta inteira sem revisao de PII).
/// - Nunca guarda a resposta do modelo nem stack trace -- so ErrorCode
///   (o mesmo Error.Code que os handlers ja usam), nunca mensagem livre.
/// Gravado de forma best-effort pelo handler (ver IAssistantQueryLogger) --
/// falha ao logar nunca pode derrubar a resposta ao usuario.
/// </summary>
public sealed class AssistantQueryLogEntry : AggregateRoot<AssistantQueryLogEntryId>, ITenantOwned
{
    private const int QuestionExcerptMaxLength = 200;

    public TenantId TenantId { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public string QuestionExcerpt { get; private set; } = string.Empty;

    public string? ResolvedIntent { get; private set; }

    public string Source { get; private set; } = string.Empty;

    public string? ToolOrServiceUsed { get; private set; }

    public bool Success { get; private set; }

    public string? ErrorCode { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    private AssistantQueryLogEntry()
    {
    }

    private AssistantQueryLogEntry(
        TenantId tenantId, Guid userId, string questionExcerpt, string? resolvedIntent, string source,
        string? toolOrServiceUsed, bool success, string? errorCode, DateTimeOffset occurredAtUtc)
        : base(AssistantQueryLogEntryId.New())
    {
        TenantId = tenantId;
        UserId = userId;
        QuestionExcerpt = questionExcerpt;
        ResolvedIntent = resolvedIntent;
        Source = source;
        ToolOrServiceUsed = toolOrServiceUsed;
        Success = success;
        ErrorCode = errorCode;
        OccurredAtUtc = occurredAtUtc;
    }

    public static AssistantQueryLogEntry Create(
        TenantId tenantId, Guid userId, string question, string? resolvedIntent, string source,
        string? toolOrServiceUsed, bool success, string? errorCode, DateTimeOffset occurredAtUtc)
    {
        var excerpt = question.Length > QuestionExcerptMaxLength ? question[..QuestionExcerptMaxLength] : question;
        return new AssistantQueryLogEntry(tenantId, userId, excerpt, resolvedIntent, source, toolOrServiceUsed, success, errorCode, occurredAtUtc);
    }
}
