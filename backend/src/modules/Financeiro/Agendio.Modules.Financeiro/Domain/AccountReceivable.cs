using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Financeiro.Domain;

/// <summary>
/// Uma entrada de receita. Nasce automaticamente (Pending) quando um agendamento
/// e concluido (ver FinancialIntegrationEventConsumer) — o Agendio nao processa o
/// pagamento do atendimento em si (so a assinatura da plataforma, via Asaas), o
/// dono confirma o recebimento manualmente. SourceAppointmentId nulo = lancamento
/// manual (nao existe hoje, mas o campo ja fica pronto para o caso).
/// </summary>
public sealed class AccountReceivable : AggregateRoot<AccountReceivableId>, ITenantOwned, IAuditable, ISoftDeletable
{
    public TenantId TenantId { get; private set; } = null!;

    public string Description { get; private set; } = string.Empty;

    public Money Amount { get; private set; } = null!;

    public DateOnly DueDate { get; private set; }

    public AccountReceivableStatus Status { get; private set; }

    public DateTimeOffset? ReceivedAtUtc { get; private set; }

    /// <summary>Agendamento que gerou esta receita — null para lancamentos manuais.</summary>
    public Guid? SourceAppointmentId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    private AccountReceivable()
    {
    }

    private AccountReceivable(TenantId tenantId, string description, Money amount, DateOnly dueDate, Guid? sourceAppointmentId)
        : base(AccountReceivableId.New())
    {
        TenantId = tenantId;
        Description = description;
        Amount = amount;
        DueDate = dueDate;
        SourceAppointmentId = sourceAppointmentId;
        Status = AccountReceivableStatus.Pending;
    }

    public static Result<AccountReceivable> Create(
        TenantId tenantId, string? description, Money amount, DateOnly dueDate, Guid? sourceAppointmentId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure<AccountReceivable>(Error.Validation("AccountReceivable.DescriptionEmpty", "A descricao nao pode ser vazia."));
        }

        return Result.Success(new AccountReceivable(tenantId, description.Trim(), amount, dueDate, sourceAppointmentId));
    }

    public Result MarkReceived(DateTimeOffset nowUtc)
    {
        if (Status != AccountReceivableStatus.Pending)
        {
            return Result.Failure(Error.Validation(
                "AccountReceivable.InvalidTransition", "So e possivel confirmar recebimento de uma conta Pendente."));
        }

        Status = AccountReceivableStatus.Received;
        ReceivedAtUtc = nowUtc;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status != AccountReceivableStatus.Pending)
        {
            return Result.Failure(Error.Validation("AccountReceivable.InvalidTransition", "So e possivel cancelar uma conta Pendente."));
        }

        Status = AccountReceivableStatus.Cancelled;
        return Result.Success();
    }
}
