namespace Agendio.Modules.Customers.Contracts;

/// <summary>
/// Contagem simples de clientes -- usada pelo Assistente (fast-path e tool de
/// IA) pra responder "quantos clientes eu tenho" sem precisar paginar
/// ListActiveBySegmentAsync so pra contar.
/// </summary>
public interface ICustomerDirectoryLookupService
{
    Task<CustomerDirectorySummaryLookupResult> GetSummaryAsync(CancellationToken cancellationToken = default);
}

public sealed record CustomerDirectorySummaryLookupResult(int TotalActiveCount, int TotalInactiveCount);
