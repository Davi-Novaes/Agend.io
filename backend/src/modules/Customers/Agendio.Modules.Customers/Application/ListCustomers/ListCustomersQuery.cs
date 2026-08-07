using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Customers.Application.ListCustomers;

public sealed record ListCustomersQuery(string? Search, int Page = 1, int PageSize = 20) : IQuery<ListCustomersResult>;

public sealed record CustomerSummary(
    Guid Id, string FullName, string? Email, string? Phone, bool IsActive, DateTimeOffset CreatedAtUtc);

public sealed record ListCustomersResult(IReadOnlyList<CustomerSummary> Items, int TotalCount, int Page, int PageSize);
