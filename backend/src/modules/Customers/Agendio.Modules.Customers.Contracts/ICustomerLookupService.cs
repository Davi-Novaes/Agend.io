namespace Agendio.Modules.Customers.Contracts;

/// <summary>
/// Unico ponto de leitura sincrona que outro modulo tem sobre Customers (ver
/// regra de dependencia em CLAUDE.md). O tenant e resolvido de forma ambiente
/// pelo global query filter do CustomersDbContext — nao precisa ser passado aqui.
/// </summary>
public interface ICustomerLookupService
{
    Task<CustomerLookupResult?> FindByIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Busca em lote — evita N+1 quando quem chama ja tem uma lista de Ids (ver BL-20, docs/BACKLOG.md).</summary>
    Task<IReadOnlyList<CustomerLookupResult>> FindByIdsAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken cancellationToken = default);

    /// <summary>Clientes ativos com e-mail cadastrado — usado por Marketing para montar a lista de destinatarios de uma campanha.</summary>
    Task<IReadOnlyList<CustomerLookupResult>> ListActiveWithEmailAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clientes ativos, opcionalmente filtrados por segmento (Fase 21 — publico-alvo
    /// de campanha). Segmento null = todos os clientes ativos. Quem chama decide
    /// qual canal usar por cliente, filtrando por Email/Phone != null no resultado.
    /// </summary>
    Task<IReadOnlyList<CustomerLookupResult>> ListActiveBySegmentAsync(CustomerSegment? segment, CancellationToken cancellationToken = default);
}
