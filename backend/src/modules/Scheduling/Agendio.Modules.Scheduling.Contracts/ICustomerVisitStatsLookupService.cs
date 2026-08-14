namespace Agendio.Modules.Scheduling.Contracts;

/// <summary>
/// Leitura em lote (todo o tenant, sem paginacao — mesma escala assumida por
/// ICustomerLookupService.ListActiveWithEmailAsync) usada pela Fase 9 (auto-
/// segmentacao de clientes) para classificar cada cliente sem que Customers
/// precise ler a tabela de agendamentos diretamente.
/// </summary>
public interface ICustomerVisitStatsLookupService
{
    Task<IReadOnlyList<CustomerVisitStatsLookupResult>> ListAllAsync(CancellationToken cancellationToken = default);
}

public sealed record CustomerVisitStatsLookupResult(
    Guid CustomerId,
    int TotalVisits,
    DateTimeOffset? LastVisitAtUtc,
    int NoShowCount,
    decimal TotalSpent);
