using Agendio.Modules.Financeiro.Domain;
using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Financeiro.Application.ListAccountsReceivable;

public sealed record ListAccountsReceivableQuery(
    AccountReceivableStatus? Status, DateOnly? From, DateOnly? To, int Page = 1, int PageSize = 20) : IQuery<ListAccountsReceivableResult>;

public sealed record AccountReceivableSummary(
    Guid Id, string Description, decimal Amount, string Currency, DateOnly DueDate,
    AccountReceivableStatus Status, DateTimeOffset? ReceivedAtUtc, Guid? SourceAppointmentId);

public sealed record ListAccountsReceivableResult(IReadOnlyList<AccountReceivableSummary> Items, int TotalCount, int Page, int PageSize);
