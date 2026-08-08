using Agendio.Modules.Financeiro.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Financeiro.Application.ListAccountsPayable;

public sealed record ListAccountsPayableQuery(
    AccountPayableStatus? Status, ExpenseCategory? Category, DateOnly? From, DateOnly? To, int Page = 1, int PageSize = 20)
    : IQuery<ListAccountsPayableResult>;

public sealed record AccountPayableSummary(
    Guid Id, string Description, decimal Amount, string Currency, DateOnly DueDate, ExpenseCategory Category,
    AccountPayableStatus Status, DateTimeOffset? PaidAtUtc, Guid? ResourceId, Guid? SourceAppointmentId);

public sealed record ListAccountsPayableResult(IReadOnlyList<AccountPayableSummary> Items, int TotalCount, int Page, int PageSize);
