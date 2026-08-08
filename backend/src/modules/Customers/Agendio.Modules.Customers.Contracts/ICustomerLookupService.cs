namespace Agendio.Modules.Customers.Contracts;

/// <summary>
/// Unico ponto de leitura sincrona que outro modulo tem sobre Customers (ver
/// regra de dependencia em CLAUDE.md). O tenant e resolvido de forma ambiente
/// pelo global query filter do CustomersDbContext — nao precisa ser passado aqui.
/// </summary>
public interface ICustomerLookupService
{
    Task<CustomerLookupResult?> FindByIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Clientes ativos com e-mail cadastrado — usado por Marketing para montar a lista de destinatarios de uma campanha.</summary>
    Task<IReadOnlyList<CustomerLookupResult>> ListActiveWithEmailAsync(CancellationToken cancellationToken = default);
}
