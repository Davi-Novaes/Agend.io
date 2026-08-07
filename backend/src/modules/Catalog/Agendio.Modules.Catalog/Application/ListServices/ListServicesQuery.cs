using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Catalog.Application.ListServices;

public sealed record ListServicesQuery(string? Search, int Page = 1, int PageSize = 20) : IQuery<ListServicesResult>;

public sealed record ServiceSummary(
    Guid Id, string Name, int DurationMinutes, decimal Price, string Currency, string? Category, bool IsActive);

public sealed record ListServicesResult(IReadOnlyList<ServiceSummary> Items, int TotalCount, int Page, int PageSize);
