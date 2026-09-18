using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;

namespace Agendio.Modules.Feedback.Domain;

/// <summary>
/// Feedback livre enviado por um usuario autenticado (qualquer papel — pedido
/// explicito do usuario, 2026-09-05) sobre o sistema. Lido pelo painel do
/// Super Admin (Platform) — ver Agendio.Modules.Feedback.Contracts.IFeedbackReader.
///
/// Deliberadamente NAO implementa ITenantOwned e NAO tem Row Level Security —
/// mesma excecao ja aplicada a Subscription/Payment/Plan (Billing) e a
/// audit_log (Identity/Platform/Customers, ver migracao RemoveAuditLogRls):
/// agendio_owner e agendio_app sao ambos NOBYPASSRLS, entao RLS aqui cegaria o
/// painel Super Admin pra feedback de qualquer tenant. TenantId continua
/// gravado (resolvido pra nome de estabelecimento na leitura), so nao filtra
/// mais a query.
/// </summary>
public sealed class FeedbackEntry : AggregateRoot<FeedbackEntryId>, IAuditable
{
    public TenantId TenantId { get; private set; } = null!;

    public Guid SubmittedByUserId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    private FeedbackEntry()
    {
    }

    private FeedbackEntry(TenantId tenantId, Guid submittedByUserId, string subject, string body)
        : base(FeedbackEntryId.New())
    {
        TenantId = tenantId;
        SubmittedByUserId = submittedByUserId;
        Subject = subject;
        Body = body;
    }

    public static Result<FeedbackEntry> Create(TenantId tenantId, Guid submittedByUserId, string? subject, string? body)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Result.Failure<FeedbackEntry>(Error.Validation("FeedbackEntry.SubjectRequired", "Informe o assunto."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure<FeedbackEntry>(Error.Validation("FeedbackEntry.BodyRequired", "Informe a mensagem."));
        }

        return Result.Success(new FeedbackEntry(tenantId, submittedByUserId, subject.Trim(), body.Trim()));
    }
}
