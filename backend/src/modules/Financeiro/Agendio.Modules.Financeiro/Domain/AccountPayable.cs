using Agendio.SharedKernel.Auditing;
using Agendio.SharedKernel.Multitenancy;
using Agendio.SharedKernel.Primitives;
using Agendio.SharedKernel.Results;
using Agendio.SharedKernel.ValueObjects;

namespace Agendio.Modules.Financeiro.Domain;

/// <summary>
/// Uma entrada de despesa — lancada manualmente (aluguel, insumos...) ou
/// automaticamente pelo FinancialIntegrationEventConsumer quando um agendamento
/// concluido tem uma CommissionRule ativa para o profissional (Category.Commission,
/// ResourceId e SourceAppointmentId preenchidos). Despesa manual: ResourceId e
/// SourceAppointmentId ficam null.
/// </summary>
public sealed class AccountPayable : AggregateRoot<AccountPayableId>, ITenantOwned, IAuditable, ISoftDeletable
{
    public TenantId TenantId { get; private set; } = null!;

    public string Description { get; private set; } = string.Empty;

    public Money Amount { get; private set; } = null!;

    public DateOnly DueDate { get; private set; }

    public ExpenseCategory Category { get; private set; }

    public AccountPayableStatus Status { get; private set; }

    public DateTimeOffset? PaidAtUtc { get; private set; }

    public Guid? ResourceId { get; private set; }

    /// <summary>Agendamento que originou esta comissao — null para despesas manuais.</summary>
    public Guid? SourceAppointmentId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }

    private AccountPayable()
    {
    }

    private AccountPayable(
        TenantId tenantId, string description, Money amount, DateOnly dueDate, ExpenseCategory category,
        Guid? resourceId, Guid? sourceAppointmentId)
        : base(AccountPayableId.New())
    {
        TenantId = tenantId;
        Description = description;
        Amount = amount;
        DueDate = dueDate;
        Category = category;
        ResourceId = resourceId;
        SourceAppointmentId = sourceAppointmentId;
        Status = AccountPayableStatus.Pending;
    }

    public static Result<AccountPayable> Create(
        TenantId tenantId, string? description, Money amount, DateOnly dueDate, ExpenseCategory category,
        Guid? resourceId = null, Guid? sourceAppointmentId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure<AccountPayable>(Error.Validation("AccountPayable.DescriptionEmpty", "A descricao nao pode ser vazia."));
        }

        return Result.Success(new AccountPayable(tenantId, description.Trim(), amount, dueDate, category, resourceId, sourceAppointmentId));
    }

    public Result MarkPaid(DateTimeOffset nowUtc)
    {
        if (Status != AccountPayableStatus.Pending)
        {
            return Result.Failure(Error.Validation("AccountPayable.InvalidTransition", "So e possivel confirmar pagamento de uma conta Pendente."));
        }

        Status = AccountPayableStatus.Paid;
        PaidAtUtc = nowUtc;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status != AccountPayableStatus.Pending)
        {
            return Result.Failure(Error.Validation("AccountPayable.InvalidTransition", "So e possivel cancelar uma conta Pendente."));
        }

        Status = AccountPayableStatus.Cancelled;
        return Result.Success();
    }
}
