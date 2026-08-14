using Agendio.SharedKernel.Messaging;

namespace Agendio.Modules.Customers.Application.ListCustomers;

public sealed record ListCustomersQuery(string? Search, CustomerSegment? Segment, int Page = 1, int PageSize = 20) : IQuery<ListCustomersResult>;

/// <summary>Fase 9 — auto-segmentacao, calculado na leitura (nunca persistido em Customer).</summary>
public enum CustomerSegment
{
    Novo,
    Recorrente,
    Vip,
    EmRisco,
    Inativo,
    NoShow,
}

public sealed record CustomerSummary(
    Guid Id, string FullName, string? Email, string? Phone, bool IsActive, DateTimeOffset CreatedAtUtc, CustomerSegment Segment);

public sealed record ListCustomersResult(IReadOnlyList<CustomerSummary> Items, int TotalCount, int Page, int PageSize);
